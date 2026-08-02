using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text;

using Messaging.Shared.Models;
using Messaging.Server.Protocols;
using Messaging.Server.Services;
using Messaging.Server.Data;
using Messaging.Server.Protocols.Handlers;


namespace Messaging.Server;

public class MessageServer {

    public int Port { get; set; }

    private readonly IServerMessageProtocol protocol;

    private readonly TcpListener listener;
    private readonly bool useTls;

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;

    private readonly ConcurrentDictionary<Guid, Task> tasks;
    private readonly MessageRouter router;
    private readonly ConcurrentDictionary<StringIdentifier, CancellationToken> tokens;
    private readonly CancellationToken ct;
    private readonly AccountDbHandler accDbHandler;

    public MessageServer(int port, IServerMessageProtocolFactory factory, bool useTls, CancellationToken ct) {
        Port = port;
        listener = new(IPAddress.Any, Port);
        this.useTls = useTls;
        handlers = new();
        tokens = new();
        
        router = new MessageRouter(handlers, new(handlers, retry: false, tokens: tokens, ct: ct));
        ServerFileStorageService service = new(Path.Combine(Environment.CurrentDirectory, "FileStorage"));
        protocol = factory.CreateProtocol([ new AckHandler(router), new TextMessageHandler(handlers, router), new FileUploadHandler(handlers, router, service), new FileRequestHandler(handlers, router, service) ]);
        tasks = [ ];
        this.ct = ct;
        accDbHandler = new();
    }


    public async Task RunAsync() {
        listener.Start();
        Console.WriteLine("Listen started");
        Task sweepTask = router.StartUnsentSweepAsync(ct);

        _ = Task.Run(async () => {
            while (!ct.IsCancellationRequested) {
                try {
                    string saveDir = Path.Combine(Environment.CurrentDirectory, "FileStorage");
                    if (Directory.Exists(saveDir)) {
                        foreach (var file in Directory.GetFiles(saveDir)) {
                            if (File.GetCreationTimeUtc(file) < DateTime.UtcNow.AddHours(-24)) {
                                File.Delete(file);
                            }
                        }
                    }
                } catch { }
                await Task.Delay(TimeSpan.FromHours(1), ct);
            }
        });

        while (!ct.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync(ct);
                Console.WriteLine("Connection accepted");
            }
            catch (OperationCanceledException) {
                Console.WriteLine("TCP listening cancelled");
                break;
            }

            // store tasks for later to await before exiting
            // assign guid so tasks can remove themselves when they finish on their own
            Guid taskId = Guid.NewGuid();
            tasks[taskId] = TrackedHandleConnectionAsync(client, taskId, ct);
            
        }

        listener.Stop();
        listener.Dispose();

        foreach (Task task in tasks.Values.ToArray()) await task;
        await sweepTask;

    }

    // calls HandleConnectionAsync and removes associated task if the call returns
    private async Task TrackedHandleConnectionAsync(TcpClient client, Guid taskId, CancellationToken ct) {
        try {
            await HandleConnectionAsync(client, ct);
        }
        catch (Exception e) {
            Console.WriteLine($"Connection handler threw exception: {e}");
        }
        finally {
            tasks.TryRemove(taskId, out _);
        }

    }
        
    // Handle introduction then start processing incoming and outgoing
    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct) {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        MessageConnection conn = new(client, useTls, linked.Token);

        using CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        if (useTls) {
            if (!await ServerAuthTlsAsync(conn, introCts.Token))
                return;
            Console.WriteLine("TLS authentication successful");
        }

        introCts.TryReset();
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        Task connTask;

        connTask = conn.StartAsync();


        //Handle introduction

        bool introduced;
        MessageConnectionHandler handler = new(conn, linked.Token);

        try {
            await handler.WaitForIncomingAsync(introCts.Token);
            introduced = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            introduced = false;
        }


        if (!introduced) {
            Console.WriteLine("Introduction not received or operation cancelled");
            linked.Cancel();
            await connTask;
            return;
        }

        introCts.TryReset();
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        MessageData firstMessage = await handler.ReadOneIncomingAsync(introCts.Token);

        StringIdentifier id = firstMessage.SourceId;
        string password = Encoding.UTF8.GetString(firstMessage.Payload);

        bool loginSuccess;
        string rejectReason = "";
        
        switch (firstMessage.Type) {
            case MessageType.Register:
                loginSuccess = await accDbHandler.RegisterUserAsync(id.Value, password);
                if (!loginSuccess) rejectReason = "User already exists";
                break;

            case MessageType.Login:
                try {
                    loginSuccess = await accDbHandler.ValidatePasswordAsync(id.Value, password);
                    if (!loginSuccess) rejectReason = "Invalid password";
                }
                catch (InvalidOperationException) {
                    loginSuccess = false;
                    rejectReason = "User does not exist";
                }
                break;
                
            default:
                loginSuccess = false;
                break;
        }

        if (!loginSuccess) {
            Console.WriteLine($"Unsuccessful login/register, reason: {rejectReason}");
            await conn.WriteAsync(protocol.CreateNack(new("SYSTEM"), id, 0, rejectReason));
            linked.Cancel();
            await connTask;
            return;
        }

        tokens.TryAdd(id, linked.Token);

        Console.WriteLine($"Login successful, ID: {id.Value}");

        handler.UserId = id;

        // add id-handler pair to active connections
        handlers.TryAdd(id, handler);
        Task handlerTask;

        try {
            Console.WriteLine("Replying ack");
            await handler.WriteToOutBufferAsync(protocol.CreateAck(new StringIdentifier("SYSTEM"), id, 0));
            // start listening for incoming and outgoing
            handlerTask = handler.StartProcessingAsync(protocol);
            // deliver pending messages
            await router.DeliverPendingMessagesAsync(id, handler);
            // process messages until shutdown
            await handlerTask;
        }
        finally {
            handlers.TryRemove(id, out _);
            tokens.TryRemove(id, out _);
            linked.Cancel();
            await connTask;
        }
    }

    public static async Task<bool> ServerAuthTlsAsync(MessageConnection conn, CancellationToken ct) {
        // get pfx location and password from evnrionment variables
        string password = Environment.GetEnvironmentVariable("PFX_PASSWORD") ?? throw new InvalidOperationException("PFX_PASSWORD is not set");
        string certificatePath = Environment.GetEnvironmentVariable("PFX_LOCATION") ?? throw new InvalidOperationException("PFX_LOCATION is not set");

        // load certificate from pfx
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            password);

        if (conn.Stream is not SslStream sslStream) {
            Console.WriteLine("stream is null when authenticating as server");
            return false;
        }

        // authenticate as server with the certificate
        try {
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions() {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12
            }, ct);
        }
        catch (Exception e) {
            Console.WriteLine($"Exception when authenticating as server: {e.Message}");
            return false;
        }

        return true;

    }
}