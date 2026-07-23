using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Messaging.Shared.Models;
using Messaging.Server.Protocols;
using Messaging.Server.Services;


namespace Messaging.Server;

public class MessageServer {

    public int Port { get; set; }

    private readonly IServerMessageProtocol protocol;

    private readonly TcpListener listener;

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;

    private readonly ConcurrentDictionary<Guid, Task> tasks;
    private readonly MessageRouter router;
    private readonly ConcurrentDictionary<StringIdentifier, CancellationToken> tokens;
    private readonly CancellationToken ct;

    public MessageServer(int port, IServerMessageProtocolFactory factory, CancellationToken ct) {
        Port = port;
        listener = new(IPAddress.IPv6Any, Port);
        handlers = new();
        tokens = new();
        
        router = new MessageRouter(handlers, new(handlers, false, tokens, ct));
        
        protocol = factory.CreateProtocol(0, handlers, router);
        tasks = [ ];
        this.ct = ct;
    }

    public async Task RunAsync() {
        listener.Start();
        Console.WriteLine("Listen started");
        Task sweepTask = router.StartUnsentSweepAsync(ct);

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
        using CancellationTokenSource cts = new();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

        MessageConnection conn = new(client, linked.Token);

        Task connTask;

        connTask = conn.StartAsync();

        using CancellationTokenSource introCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        //Handle introduction

        bool introduced;
        MessageConnectionHandler handler = new(conn, linked);

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

        introCts.TryReset();
        introCts.CancelAfter(TimeSpan.FromSeconds(5));

        StringIdentifier id = protocol.ReceiveIntroduction(await handler.ReadOneIncomingAsync(introCts.Token));
        tokens.TryAdd(id, linked.Token);

        Console.WriteLine($"Introduction received, ID: {id.Value}");

        // add id-handler pair to active connections
        handlers.TryAdd(id, handler);
        Task handlerTask;

        try {
            Console.WriteLine("Replying ack");
            await handler.WriteToOutBufferAsync(protocol.CreateAck(new StringIdentifier("SYSTEM"), id, 0));
            Console.WriteLine("Ack added to buffer");
            // start listening for incoming and outgoing
            handlerTask = handler.StartProcessingAsync(protocol);
            // deliver pending messages
            await router.DeliverPendingMessagesAsync(id, handler);
            // process messages until shutdown
            await handlerTask;
        }
        finally {
            handlers.TryRemove(id, out _);
            cts.Cancel();
            await connTask;
        }
    }
}