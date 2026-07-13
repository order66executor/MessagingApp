using System.Net;
using System.Net.Sockets;

using Messaging.Shared;
using Messaging.Shared.Protocols;

namespace Messaging.Client;

public class MessageClient {

    private readonly IPAddress address;
    private readonly int port;
    private readonly IMessageProtocol protocol;

    private readonly TcpClient client;
    private readonly MessageDataBuffer outBuffer;

    private MessageConnection? conn;

    public MessageClient(IPAddress address, int port, IMessageProtocol protocol) {
        this.address = address;
        this.port = port;
        this.protocol = protocol;
        outBuffer = new();
        client = new(AddressFamily.InterNetwork);
    }

    public async Task RunAsync(CancellationToken ct) {
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Console.Write("Enter username: ");
        string? username;

        while ((username = Console.ReadLine()) is null);

        StringIdentifier selfId = new(username);

        client.Connect(address, port);
        conn = new(client, linked.Token);

        Task connTask = conn.StartAsync();

        bool introduced;
        try {
            await conn.Buffer.Writer.WriteAsync(protocol.CreateIntroduction(selfId));
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Introduction cancelled");
        }


        try {
            introduced = (await conn.Buffer.Reader.ReadAsync(linked.Token)).Type == MessageType.Ack;
        }
        catch (OperationCanceledException) {
            Console.WriteLine("ACK Read cancelled");
            introduced = false;
        }

        MessageConnectionHandler handler;

        if (introduced) {
            handler = new(protocol, conn, linked.Token, 1, 1);
        }
        else {
            Console.WriteLine("Unsuccessful introduction");
            linked.Cancel();
            await connTask;
            return;
        }

        await handler.StartProcessingAsync();
        

        await connTask;
    }



}