using System.Net;
using System.Net.Sockets;

using Messaging.Shared;
using Messaging.Client.Protocols;
using System.Runtime.InteropServices;
using Messaging.Client.Services;

namespace Messaging.Client;

public class MessageClient {

    private readonly IPAddress address;
    private readonly int port;
    private IClientMessageProtocol? protocol;

    private readonly IClientMessageProtocolFactory factory;

    private readonly TcpClient client;

    private MessageConnection? conn;
    private MessageConnectionHandler? handler;
    private readonly string username;

    public ClientDbHandler DbHandler { get; }

    public MessageClient(IPAddress address, int port, IClientMessageProtocolFactory factory, string username) {
        this.address = address;
        this.port = port;
        this.factory = factory;
        client = new(AddressFamily.InterNetwork);
        this.username = username;
        DbHandler = new();

    }

    // Connects and introduces to server, then starts listening for incoming and outgoing messages
    public async Task RunAsync(CancellationTokenSource cts) {
        CancellationToken ct = cts.Token;
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        StringIdentifier selfId = new(username);

        // Connect
        try {
            Console.WriteLine("Attempting connection");
            await client.ConnectAsync(address, port);
        }
        catch (Exception e) {
            Console.WriteLine($"Connection failed: {e.Message}");
            client.Dispose();
            return;
        }

        Console.WriteLine("Connection successful");
        conn = new(client, linked.Token);

        // Start the connection's incoming listener
        Task connTask = conn.StartAsync();

        handler = new(conn, linked.Token);
        protocol = factory.CreateProtocol(selfId, handler, DbHandler);

        // Send intro

        try {
            await conn.WriteAsync(protocol.CreateIntroduction());
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Introduction cancelled");
        }

        //Cancel waiting for ack after 5 seconds
        CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(5000);

        bool response = false;

        // wait for ack
        try {
            response = await handler.WaitForIncomingAsync(introCts.Token);
        }
        catch (OperationCanceledException) when (!linked.IsCancellationRequested) {
            Console.WriteLine("Problem while waiting to receive ack");
        }

        if (!response) {
            Console.WriteLine("Timeout waiting for ack");
            linked.Cancel();
            await connTask;
            return;
        }
        
        
        bool introduced;

        //Check if message is actually ack
        try {
            introduced = (await handler.ReadOneIncomingAsync(linked.Token)).Type == MessageType.Ack;
        }
        catch (OperationCanceledException) {
            Console.WriteLine("ACK Read cancelled");
            introduced = false;
        }


        if (introduced) {
            Console.WriteLine("ACK Received");
        }
        else {
            Console.WriteLine("Unsuccessful introduction");
            linked.Cancel();
            await connTask;
            return;
        }

        // Start listening for incoming and outgoing messages
        await handler.StartProcessingAsync(protocol);

        await connTask;
        cts.Cancel();
    }

    // Queue a text message to the outgoing buffer
    public async Task SendTextMessageAsync(string target, string text) {
        if (protocol is null) return;
        await protocol.SendTextMessageAsync(new StringIdentifier(target), text);
    }

}