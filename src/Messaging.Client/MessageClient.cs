using System.Net;
using System.Net.Sockets;

using Messaging.Shared;
using Messaging.Client.Protocols;

namespace Messaging.Client;

public class MessageClient {

    private readonly IPAddress address;
    private readonly int port;
    private IClientMessageProtocol? protocol;

    private readonly IClientMessageProtocolFactory factory;

    private readonly TcpClient client;

    private MessageConnection? conn;
    private readonly string username;

    public MessageClient(IPAddress address, int port, IClientMessageProtocolFactory factory, string username) {
        this.address = address;
        this.port = port;
        this.factory = factory;
        client = new(AddressFamily.InterNetwork);
        this.username = username;
    }

    public async Task RunAsync(CancellationToken ct) {
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        StringIdentifier selfId = new(username);


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

        Task connTask = conn.StartAsync();

        MessageConnectionHandler handler = new(conn, linked.Token);
        protocol = factory.CreateProtocol(0, selfId, handler);

        try {
            await conn.WriteAsync(protocol.CreateIntroduction(selfId));
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Introduction cancelled");
        }

        CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(5000);

        bool response = false;
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

        await handler.StartProcessingAsync(protocol);
        

        await connTask;
    }



}