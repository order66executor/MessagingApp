using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Messaging.Shared;
using Messaging.Server.Protocols;
using Messaging.Server.Data;
using Messaging.Server.Services;

namespace Messaging.Server;

public class MessageServer {

    public int Port { get; set; }

    private readonly IServerMessageProtocol protocol;

    private readonly TcpListener listener;

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;

    private readonly ConcurrentDictionary<Guid, Task> tasks;
    private readonly MessageRouter router;

    public MessageServer(int port, IServerMessageProtocolFactory factory) {
        Port = port;
        listener = new(IPAddress.Any, Port);
        this.handlers = new();
        
        using (var db = new MessagingDbContext()) {
            db.Database.EnsureCreated();
        }
        router = new MessageRouter(handlers);
        
        protocol = factory.CreateProtocol(0, handlers, router);
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
            cts.Cancel();
            await connTask;
            return;
        }

        StringIdentifier id = protocol.ReceiveIntroduction(await handler.ReadOneIncomingAsync(linked.Token));

        Console.WriteLine($"Introduction received, ID: {id.Value}");

        handlers.TryAdd(id, handler);
        try {
            Console.WriteLine("Replying ack");
            await handler.WriteToOutBufferAsync(protocol.CreateAck(new StringIdentifier("SYSTEM"), id, 0));
            Console.WriteLine("Ack added to buffer");
            await router.DeliverPendingMessagesAsync(id, handler);
            await handler.StartProcessingAsync(protocol);
        }
        finally {
            handlers.TryRemove(id, out _);
            cts.Cancel();
            await connTask;
        }
    }
}