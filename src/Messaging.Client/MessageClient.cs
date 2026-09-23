using System.Net;
using System.Net.Sockets;

using Messaging.Shared.Models;
using Messaging.Client.Services;
using Messaging.Shared.Services;
using MessagePack;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Text;
using Messaging.Shared.Protocol;
using Messaging.Shared.Protocol.Handlers;
using Messaging.Client.Protocols.Handlers;
using System.Collections.Concurrent;

namespace Messaging.Client;

public class MessageClient {

    // IP Address of the server
    private readonly IPAddress address;

    //Port of the server
    private readonly int port;

    // Dispatcher that handles incoming messages
    private IMessageDispatcher? dispatcher;

    // TCPClient object for the connection to the server
    private readonly TcpClient client;

    // Whether to use TLS authentication when connecting to server. Turn off for testing
    private readonly bool useTls;

    // Connection object wrapping the TcpClient object
    private MessageConnection? conn;

    // Connection handler object wrapping the connection
    private MessageConnectionHandler? handler;

    // Username of this client
    private readonly StringIdentifier username;

    // Password used for logging in to the server IN PLAIN TEXT (we should probably do something about that)
    private readonly string password;

    // DB Handler object for abstracting over the DbContext
    public ClientDbHandler DbHandler { get; }

    // ACK waiting handler object that all outgoing messages (SHOULD) go through
    private AckWaitHandler? waitHandler;

    // Message sender object that routes through waitHandler
    private ClientMessageSender? sender;

    private readonly FileStorageService storageService;

    private readonly ConcurrentDictionary<Guid, string> pendingFiles;

    private readonly ConcurrentDictionary<Guid, string> fileHashes;

    public MessageClient(IPAddress address, int port, string username, string password, bool useTls) {
        this.address = address;
        this.port = port;
        client = new(AddressFamily.InterNetwork); // Use IPv4
        this.username = new(username);
        this.password = password;
        DbHandler = new();
        this.useTls = useTls;
        pendingFiles = [ ];
        fileHashes = [ ];

        string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        storageService = new(downloadsDir);
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

        waitHandler = new(handler, true, linked.Token);
        sender = new(username, DbHandler, waitHandler, handler, storageService, pendingFiles);

        dispatcher = new MessageDispatcher([ new AckHandler(waitHandler), new FileNotificationHandler(DbHandler, handler),
            new FileResponseHandler(handler, storageService, fileHashes), new TextMessageHandler(DbHandler, handler),
            new FileTransferReadyHandler(handler, storageService, pendingFiles, sender),
            new SegmentHandler(storageService, fileHashes)]);

        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        // Try logging in or registering
        if (registering) {
            if (!await TryRegisterAsync(introCts.Token)) {
                linked.Cancel();
                await connTask;
                return;
            }
        }
        else if (!await TryLoginAsync(introCts.Token)) {
            linked.Cancel();
            await connTask;
            return;
        }

        // Start listening for incoming and outgoing messages
        Task handlerTask = handler.StartProcessingAsync(dispatcher);
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
        if (sender is null) return;
        await sender.SendTextMessageAsync(new StringIdentifier(target), text);
    }

    public async Task SendFileAsync(string target, string filePath) {
        if (sender is null) return;
        await sender.SendFileUploadAsync(new StringIdentifier(target), filePath);
    }

    public async Task RequestFileAsync(string fileId) {
        if (sender is null) return;
        await sender.RequestFileAsync(fileId);
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

                // Check for null messages
                if (messageData is not null && waitHandler is not null) {

                    // Do not await sends one by one. ackHandler will take care of ordering and pacing.
                    sendTasks.Add(waitHandler.EnqueueMessageAsync(messageData));

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

            // Update messages state according to success state
            await DbHandler.UpdateMessageStateAsync(realPendingMessages[i].Id, results[i] ? MessageState.Sent : MessageState.Unsent);
        }

        if (realPendingMessages.Count > 0) {
            Console.WriteLine($"Delivered {success} pending messages, failed {failure}");
        }
    }

    // Attempt registering an account on the server
    private async Task<bool> TryRegisterAsync(CancellationToken ct) {
        if (sender is null || handler is null || conn is null) return false;

        MessageData message = sender.CreateAccountMessage(password, MessageType.Register);

        // Send registration request
        await conn.WriteAsync(message);

        MessageData response;

        // Wait for response
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

    // Attempt logging in to the server
    private async Task<bool> TryLoginAsync(CancellationToken ct) {
        if (sender is null || handler is null || conn is null) return false;
        MessageData message = sender.CreateAccountMessage(password, MessageType.Login);

        // Send login request
        await conn.WriteAsync(message);

        MessageData response;

        // Await response
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