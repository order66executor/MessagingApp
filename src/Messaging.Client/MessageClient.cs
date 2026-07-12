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
        Console.Write("Enter username: ");
        string? username;
        int messageId = 0;

        while ((username = Console.ReadLine()) is null);

        StringIdentifier selfId = new(username);

        client.Connect(address, port);
        conn = new(client, ct);

        Task connTask = conn.StartAsync();
        await protocol.IntroduceAsync(conn, ++messageId, selfId);

        int waitResult;

        if (WaitHandle.WaitAny([conn.Buffer.HasMessage, ct.WaitHandle], 5000) == 0) {
            if (conn.Buffer.TryDequeue(out MessageData data)) {
                        await protocol.ProcessAsync(data);
                    }

            else {
                Console.WriteLine("Dequeue failed");
            }
        }

        await connTask;
    }



}