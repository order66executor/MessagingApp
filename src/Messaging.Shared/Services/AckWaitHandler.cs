using System.Collections.Concurrent;

using Messaging.Shared.Models;


namespace Messaging.Shared.Services;


public class AckWaitHandler {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly CancellationToken ct;
    private readonly ConcurrentDictionary<StringIdentifier, CancellationToken> tokens;
    private readonly bool retry;
    private readonly bool hasOnlyOneHandler;
    private readonly ConcurrentDictionary<StringIdentifier, MessageDataBuffer> pendingBuffers;
    private readonly PendingAckTracker tracker;
    private MessageConnectionHandler Handler => handlers.Values.First();
    public ConcurrentDictionary<MessageData, TaskCompletionSource<bool>> InProgress { get; }
    private readonly Lock syncRoot = new();
    private readonly bool useSourceId;

    public AckWaitHandler(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers,
        bool retry, ConcurrentDictionary<StringIdentifier, CancellationToken> tokens, CancellationToken ct) {

        // used for the server
        this.ct = ct;
        this.handlers = handlers;
        this.retry = retry;
        hasOnlyOneHandler = false;
        pendingBuffers = new();
        InProgress = [ ];
        this.tokens = tokens;
        tracker = new(ct);

        //to-be-acked messages are identified by id and source
        useSourceId = true;
    }

    public AckWaitHandler(MessageConnectionHandler handler, bool retry, CancellationToken ct) {
        //used on the client
        handlers = [ ];
        tokens = [ ];
        InProgress = [ ];
        this.retry = retry;
        pendingBuffers = new();
        this.ct = ct;

        handlers.TryAdd(new StringIdentifier(""), handler);
        tokens.TryAdd(new StringIdentifier(""), ct);
        hasOnlyOneHandler = true;
        tracker = new(ct);

        //to-be-acked messages are identified by id and target
        useSourceId = false;
    }



    public async Task<bool> EnqueueMessageAsync(MessageData message) {
        CancellationToken ct;
        if (hasOnlyOneHandler) ct = this.ct;
        else tokens.TryGetValue(message.TargetId, out ct);

        MessageDataBuffer? buf;

        lock (syncRoot) {
            if (!pendingBuffers.TryGetValue(message.TargetId, out buf) && !hasOnlyOneHandler) {
                buf = new();
                pendingBuffers[message.TargetId] = buf;
                Console.WriteLine("new buffer created, ack processing started");
                _ = StartProcessingAsync(message.TargetId, ct);
            }
            else if (hasOnlyOneHandler) {
                if (pendingBuffers.IsEmpty) {
                    StringIdentifier id = new("");
                    pendingBuffers[id] = new();
                    Console.WriteLine("Single buffer created");
                    _ = StartProcessingAsync(id, ct);
                }
                buf = pendingBuffers.First().Value;
            }
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

    public async Task StartProcessingAsync(StringIdentifier id, CancellationToken ct) {

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
                    Task<bool> resultTask = tracker.RegisterWaitAsync((message.Id, useSourceId ? message.SourceId : message.TargetId));
                    try {
                        await handler.WriteToOutBufferAsync(message);
                    }
                    catch (OperationCanceledException) {
                    }

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
        catch(OperationCanceledException) {
            Console.WriteLine("Ack waiting has cancelled");
        }
        finally {
            while (buffer.Reader.TryRead(out var message)) {

                InProgress.TryGetValue(message, out var tcs);
                if (tcs is null) {
                    Console.WriteLine("Cannot complete ack wait, tcs is null");
                    continue;
                }
                Console.WriteLine("Ack waiting results set to false");
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