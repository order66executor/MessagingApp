using System.Net;
using System.Net.Sockets;

using Messaging.Shared.Models;
using Messaging.Client.Protocols;
using Messaging.Client.Services;
using Messaging.Shared.Services;
using MessagePack;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Text;

namespace Messaging.Client;

public class MessageClient {

    private readonly IPAddress address;
    private readonly int port;
    private IClientMessageProtocol? protocol;

    private readonly IClientMessageProtocolFactory factory;

    private readonly TcpClient client;
    private readonly bool useTls;

    private MessageConnection? conn;
    private MessageConnectionHandler? handler;
    private readonly StringIdentifier username;
    private readonly string password;
    public ClientDbHandler DbHandler { get; }
    private AckWaitHandler? ackHandler;

    public MessageClient(IPAddress address, int port, IClientMessageProtocolFactory factory, string username, string password, bool useTls) {
        this.address = address;
        this.port = port;
        this.factory = factory;
        client = new(AddressFamily.InterNetwork);
        this.username = new(username);
        this.password = password;
        DbHandler = new();
        this.useTls = useTls;
    }

    // Attempt connecting to server
    private async Task<bool> TryConnectAsync(CancellationToken ct) {
        try {
            Console.WriteLine("Attempting connection");
            await client.ConnectAsync(address, port, ct);
        }
        catch (Exception e) {
            Console.WriteLine($"Connection failed: {e.Message}");
            client.Dispose();
            return false;
        }

        return true;
    }

    // Connects and introduces to server, then starts listening for incoming and outgoing messages
    public async Task RunAsync(CancellationToken ct, bool registering = false) {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        using CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        // Connect
        if (!await TryConnectAsync(introCts.Token))
            return;

        introCts.TryReset();

        Console.WriteLine("Connection successful");
        conn = new(client, useTls, linked.Token);

        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        if (useTls) {
            if (!await AuthTlsAsync(introCts.Token))
                return;
            Console.WriteLine("TLS authentication successful");
        }

        introCts.TryReset();

        // Start the connection's incoming listener
        Task connTask = conn.StartAsync();

        handler = new(conn, linked.Token) {
            UserId = new("SYSTEM")
        };
        ackHandler = new(handler, true, linked.Token);
        protocol = factory.CreateProtocol(username, handler, DbHandler, ackHandler);
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        // Try logging in or registering
        if (registering)
            if (!await TryRegisterAsync(password, introCts.Token)) {
                linked.Cancel();
                await connTask;
                return;
            }
        else if (!await TryLoginAsync(password, introCts.Token)) {
            linked.Cancel();
            await connTask;
            return;
        }

        // Start listening for incoming and outgoing messages
        Task handlerTask = handler.StartProcessingAsync(protocol);
        await SendUnsentMessagesAsync();

        await handlerTask;
        linked.Cancel();
        await connTask;
    }

    private async Task<bool> AuthTlsAsync(CancellationToken ct) {
        if (conn is null) {
            Console.WriteLine("Conn is null when authenticating");
            return false;
        }
        if (conn.Stream is not SslStream sslStream) {
            Console.WriteLine("stream is null when authenticating");
            return false;
        }

        // load the certificate as bytes from the assembly
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Messaging.Client.Certificates.ca.crt");
        if (stream is null) {
            Console.WriteLine("CA cert could not be loaded");
            return false;
        }
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        // load certificate from bytes
        var ca = X509CertificateLoader.LoadCertificate(ms.ToArray());

        try {
            // athenticate as client with the CA certificate
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions() {
                TargetHost = "msgserver.public",
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateChainPolicy = new X509ChainPolicy() {
                    TrustMode = X509ChainTrustMode.CustomRootTrust,
                    CustomTrustStore = { ca },
                    RevocationMode = X509RevocationMode.NoCheck
                }

            }, ct);
        }
        catch (Exception e) {
            Console.WriteLine($"Error authenticating: {e.Message}");
            return false;
        }

        return true;

    }

    // Queue a text message to the outgoing buffer
    public async Task SendTextMessageAsync(string target, string text) {
        if (protocol is null) return;
        await protocol.SendTextMessageAsync(new StringIdentifier(target), text);
    }

    public async Task SendFileAsync(string target, string filePath) {
        if (protocol is null) return;
        await protocol.SendFileAsync(new StringIdentifier(target), filePath);
    }

    public async Task RequestFileAsync(string fileId) {
        if (protocol is null) return;
        await protocol.RequestFileAsync(fileId);
    }

    private async Task SendUnsentMessagesAsync() {
        // Get messages of the current user that are unsent
        MessageWrapper[] wrappers = await DbHandler.GetMessagesWithStateAsync(username.Value, MessageState.Unsent);

        List<Task<bool>> sendTasks = [ ];
        List<MessageWrapper> realPendingMessages = [ ];

        foreach (var wrapper in wrappers) {
            wrapper.State = MessageState.Pending;

            try {
                MessageData? messageData = MessagePackSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
                if (messageData is not null && ackHandler is not null) {
                    // Do not await sends one by one. ackHandler will take care of ordering and pacing.
                    sendTasks.Add(ackHandler.EnqueueMessageAsync(messageData));
                    Console.WriteLine("Pending message enqueued");
                    realPendingMessages.Add(wrapper);
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Error delivering pending message to: {e.Message}");
            }
        }

        // await send to finish

        bool[] results = await Task.WhenAll(sendTasks);
        int success = 0, failure = 0;

        for (int i = 0; i < sendTasks.Count; ++i) {
            if (results[i]) {
                ++success;
            }
            else {
                ++failure;
            }
            await DbHandler.UpdateMessageStateAsync(realPendingMessages[i].Id, results[i] ? MessageState.Sent : MessageState.Unsent);
        }

        if (realPendingMessages.Count > 0) {
            Console.WriteLine($"Delivered {success} pending messages, failed {failure}");
        }
    }

    private async Task<bool> TryRegisterAsync(string password, CancellationToken ct) {
        if (protocol is null || handler is null || conn is null) return false;
        MessageData message = protocol.CreateAccountMessage(password, MessageType.Register);



        await conn.Buffer.Writer.WriteAsync(message, ct);

        MessageData response;

        try {
            response = await handler.ReadOneIncomingAsync(ct);
        } 
        catch (Exception e) {
            Console.WriteLine($"Failed to read registration response: {e.Message}");
            return false;
        }

        switch (response.Type) {
            case MessageType.Ack:
                Console.WriteLine("Successful registration");
                return true;
            case MessageType.Nack:
                Console.WriteLine($"Unsuccessful registration: {Encoding.UTF8.GetString(response.Payload)}");
                return false;
            default:
                Console.WriteLine($"Unsuccessful registration, response type was: {response.Type}");
                return false;
        }

        
    }

    private async Task<bool> TryLoginAsync(string password, CancellationToken ct) {
        if (protocol is null || handler is null || conn is null) return false;
        MessageData message = protocol.CreateAccountMessage(password, MessageType.Login);

        await conn.Buffer.Writer.WriteAsync(message, ct);

        MessageData response;

        try {
            response = await handler.ReadOneIncomingAsync(ct);
        }
        catch (Exception e) {
            Console.WriteLine($"Login failed: {e.Message}");
            return false;
        }

        switch (response.Type) {
            case MessageType.Ack:
                Console.WriteLine("Successful login");
                return true;
            case MessageType.Nack:
                Console.WriteLine($"Unsuccessful login: {Encoding.UTF8.GetString(response.Payload)}");
                return false;
            default:
                Console.WriteLine($"Unsuccessful login, response type was: {response.Type}");
                return false;


        }

    }
}