using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Messaging.Shared;
using Messaging.Shared.Protocols;

namespace Messaging.Server;

public class MessageServer {

    public int Port { get; set; }

    private readonly IMessageProtocol protocol;

    private readonly TcpListener listener;

    private readonly Dictionary<StringIdentifier, MessageConnection> users;
    private readonly Dictionary<MessageConnection, MessageDataBuffer> outgoingBuffers;

    private readonly List<Task> tasks;

    public MessageServer(int port, IMessageProtocol protocol) {
        Port = port;
        this.protocol = protocol;
        listener = new(IPAddress.Any, Port);
        users = [ ];
        tasks = [ ];
        outgoingBuffers = [ ];
    }

    public async Task RunAsync(CancellationToken ct) {
        listener.Start();

        while (!ct.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) {
                Console.WriteLine("TCP listening cancelled");
                break;
            }

            tasks.Add(HandleConnectionAsync(client, ct));
            
        }

        listener.Stop();
        listener.Dispose();

        foreach (Task task in tasks) await task;

    }

    public async Task HandleConnectionAsync(TcpClient client, CancellationToken ct) {
        CancellationTokenSource cts = new();

        MessageConnection conn = new(client, cts.Token);

        Task connTask;

        connTask = conn.StartAsync();

        int waitResult;
        MessageDataBuffer buffer = new();

        int messageId = 0;


        if ((waitResult = WaitHandle.WaitAny([conn.Buffer.HasMessage, ct.WaitHandle], 5000)) == 0) {
            StringIdentifier id = await protocol.ReceiveIntroductionAsync(conn);
            Console.WriteLine($"Introduction received, ID: {id.Value}");

            users.Add(id, conn);
            outgoingBuffers.Add(conn, buffer);

            await protocol.SendAckAsync(conn, --messageId, new StringIdentifier("SYSTEM"), id, 1);
        }
        else {
            Console.WriteLine("Introduction not received or operation cancelled");
            cts.Cancel();
        }

        //TODO: make concurrent
        while (!ct.IsCancellationRequested) {
            if ((waitResult = WaitHandle.WaitAny([conn.Buffer.HasMessage, buffer.HasMessage, ct.WaitHandle])) == 0) {
                while (conn.Buffer.Count > 0) {

                    

                }
            }
            else if (waitResult == 1) {
                while (buffer.Count > 0 && !ct.IsCancellationRequested) {

                    if (buffer.TryDequeue(out MessageData data)) {
                        await conn.WriteAsync(data);
                    }

                    else {
                        Console.WriteLine("Dequeue failed");
                    }
                }
            }
        }

        cts.Cancel();

        await connTask;
    }

}