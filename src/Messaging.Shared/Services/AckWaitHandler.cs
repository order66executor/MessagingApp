using System.Collections.Concurrent;

using Messaging.Shared.Models;


namespace Messaging.Shared.Services;


public class AckWaitHandler {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly CancellationToken ct;
    private readonly bool retry;
    private readonly bool hasOnlyOneHandler;
    private readonly ConcurrentDictionary<StringIdentifier, MessageDataBuffer> pendingBuffers;
    private readonly PendingAckTracker tracker;
    private MessageConnectionHandler Handler => handlers.Values.First();
    public ConcurrentDictionary<MessageData, TaskCompletionSource<bool>> InProgress { get; }

    public AckWaitHandler(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers,
        bool retry, CancellationToken ct) {

        this.handlers = handlers;
        this.ct = ct;
        this.retry = retry;
        hasOnlyOneHandler = false;
        pendingBuffers = new();
        InProgress = [ ];
        tracker = new(ct);
    }

    public AckWaitHandler(MessageConnectionHandler handler, bool retry, CancellationToken ct) : this(handlers: new(), retry, ct) {
        handlers.TryAdd(new StringIdentifier(""), handler);
        hasOnlyOneHandler = true;
    }



    public async Task<bool> EnqueueMessage(MessageData message) {
        if (!pendingBuffers.TryGetValue(message.TargetId, out MessageDataBuffer? buf)) {
            buf = new();
            pendingBuffers.TryAdd(message.TargetId, buf);
            _ = StartProcessingAsync(message.TargetId);
        }
        if (buf is null) {
            Console.WriteLine("Buf is null in enqueueMessage");
            return false;
        }

        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InProgress.TryAdd(message, tcs);

        await buf.Writer.WriteAsync(message, ct);
        bool result = await tcs.Task;
        InProgress.Remove(message, out _);

        return result;

    }

    public async Task StartProcessingAsync(StringIdentifier id) {

        MessageConnectionHandler? handler;

        if (hasOnlyOneHandler) handler = Handler;
        else handlers.TryGetValue(id, out handler);

        if (handler is null) {
            Console.WriteLine("Handler is null, processing cannot start for ack waiting");
            return;
        }

        pendingBuffers.TryGetValue(id, out var buffer);
        if (buffer is null) {
            Console.WriteLine("Buffer is null, processing cannot start for ack waiting");
            return;
        }

        try {
            await foreach (MessageData message in buffer.Reader.ReadAllAsync(ct)) {
                bool result;
                
                do {
                    Task<bool> resultTask = tracker.RegisterWait((message.Id, message.TargetId));
                    await handler.WriteToOutBufferAsync(message);

                    result = await resultTask;
                    
                    if (!result) Console.WriteLine("ack did not arrive");
                } while (!result && retry && !ct.IsCancellationRequested);

                InProgress.TryGetValue(message, out var tcs);
                if (tcs is null) {
                    Console.WriteLine("Cannot complete ack wait, tcs is null");
                    continue;
                }
                tcs.TrySetResult(result);
            }
        }
        catch(OperationCanceledException) when (!ct.IsCancellationRequested) {
            Console.WriteLine("Ack waiting has cancelled not by token");
        }
        finally {
            while (buffer.Reader.TryRead(out var message)) {

                InProgress.TryGetValue(message, out var tcs);
                if (tcs is null) {
                    Console.WriteLine("Cannot complete ack wait, tcs is null");
                    continue;
                }
                tcs.TrySetResult(false);
            }
            pendingBuffers.Remove(id, out _);
            buffer.Dispose();
        }



    }

    public void SubmitAck(MessageData message) {
        tracker.Complete((message.Id, message.TargetId));
    }


}