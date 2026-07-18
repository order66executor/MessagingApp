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

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> users;

    private readonly ConcurrentDictionary<Guid, Task> tasks;

    public MessageServer(int port, IMessageProtocol protocol) {
        Port = port;
        this.protocol = protocol;
        listener = new(IPAddress.Any, Port);
        users = [ ];
        tasks = [ ];
    }

    public async Task RunAsync(CancellationToken ct) {
        listener.Start();
        Console.WriteLine("Listen started");

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
            Guid taskId = Guid.NewGuid();
            tasks[taskId] = TrackedHandleConnectionAsync(client, taskId, ct);
            
        }

        listener.Stop();
        listener.Dispose();

        foreach (Task task in tasks.Values.ToArray()) await task;

    }

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
        

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct) {
        using CancellationTokenSource cts = new();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

        MessageConnection conn = new(client, linked.Token);

        Task connTask;

        connTask = conn.StartAsync();

        using CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(5000);

        bool introduced;

        try {
            await conn.Buffer.Reader.WaitToReadAsync(introCts.Token);
            introduced = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            introduced = false;
        }


        if (!introduced) {
            Console.WriteLine("Introduction not received or operation cancelled");
            cts.Cancel();
            await connTask;
            return;
        }

        StringIdentifier id = protocol.ReceiveIntroduction(await conn.Buffer.Reader.ReadAsync(linked.Token));

        Console.WriteLine($"Introduction received, ID: {id.Value}");

        MessageConnectionHandler handler = new(protocol, conn, linked.Token, -1, -1);
        users.TryAdd(id, handler);
        try {
            Console.WriteLine("Replying ack");
            await handler.WriteToOutBufferAsync(protocol.CreateAck(0, new StringIdentifier("SYSTEM"), id, 1));
            Console.WriteLine("Ack added to buffer");
            await handler.StartProcessingAsync();
        }
        finally {
            users.TryRemove(id, out _);
            cts.Cancel();
            await connTask;
        }
    }
}