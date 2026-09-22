using System.Collections.Concurrent;

using Messaging.Shared.Models;


namespace Messaging.Shared.Services;

// Handles waiting for ack on both the client and the server. Requires some refactoring for clarity but functionally sound (i hope)
public class AckWaitHandler {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly CancellationToken ct;
    private readonly ConcurrentDictionary<StringIdentifier, CancellationToken> tokens;
    private readonly bool retry; // Retry sending if the send fails. Must be used on the client
    private readonly bool hasOnlyOneHandler; // If the client uses it. Probably redundant. Should merge retry and this into a single field
    private readonly ConcurrentDictionary<StringIdentifier, MessageDataBuffer> pendingBuffers; // Buffers that store messages that are pending ACK receipt. Seperate one per target to increase throughput.
    private readonly PendingAckTracker tracker; // Helper class that tracks messages and received ACKs
    private MessageConnectionHandler Handler => handlers.Values.First(); // Specific field for the first handler in the list. Used on the client side when there is only one handler
    public ConcurrentDictionary<MessageData, TaskCompletionSource<bool>> InProgress { get; } // Dict that stores MessageData and TCS pairs that are currently waiting for an ACK to arrive
    private readonly Lock syncRoot = new(); // Lock
    private readonly bool useSourceId; // Also redundant. Specifies whether the SourceId of the MessageData should be used as a key for the message in the tracker. True on the server, false on the client

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


    // Enqueue message to be sent on the corresponding buffer
    public async Task<bool> EnqueueMessageAsync(MessageData message) {

        // Set ct to be the ct for the specific user's connections. Always the same on the client
        CancellationToken ct;
        if (hasOnlyOneHandler) ct = this.ct;
        else tokens.TryGetValue(message.TargetId, out ct);

        MessageDataBuffer? buf;

        // Lock so simultaneously handled messages do not cause problems
        lock (syncRoot) {

            // If there is no buffer for the target yet create one and start asynchronously processing it (listening for placed messages).
            if (!pendingBuffers.TryGetValue(message.TargetId, out buf) && !hasOnlyOneHandler) {
                buf = new();
                pendingBuffers[message.TargetId] = buf;

                Console.WriteLine("new buffer created, ack processing started");
                _ = StartProcessingAsync(message.TargetId, ct); // Tracking is not necessary because this task is always finished before all EnqueueMessageAsync calls return
            }

            // If there is only one handler (i.e. we are on the client-side)
            // then create a buffer if there isn't one or get the only buffer
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

        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously); // TCS that will be completed when the ACK arrives or times out
        InProgress.TryAdd(message, tcs);

        await buf.Writer.WriteAsync(message, ct); // Write message to the buffer
        bool result = await tcs.Task; // Wait for ack or timeout
        InProgress.Remove(message, out _);

        return result;

    }

    // Async processing of outgoing messages placed in the buffer keyed by id
    public async Task StartProcessingAsync(StringIdentifier id, CancellationToken ct) {

        MessageConnectionHandler? handler;

        if (hasOnlyOneHandler) handler = Handler; // If we are on the client 
        else handlers.TryGetValue(id, out handler); // If not

        if (handler is null) {
            Console.WriteLine("Handler is null, processing cannot start for ack waiting");
            return;
        }

        pendingBuffers.TryGetValue(id, out var buffer); // Get the associated buffer
        if (buffer is null) {
            Console.WriteLine("Buffer is null, processing cannot start for ack waiting");
            return;
        }

        try {
            await foreach (MessageData message in buffer.Reader.ReadAllAsync(ct)) { // Process messages placed in the buffer until ct signals
                bool result;
                
                do {

                    // Register the message in the tracker with a complex key. 
                    Task<bool> resultTask = tracker.RegisterWaitAsync((message.Id, useSourceId ? message.SourceId : message.TargetId));

                    try {
                        await handler.WriteToOutBufferAsync(message); // Write message to the ConnectionHandler's outBuffer
                    }
                    catch (OperationCanceledException) {

                    }

                    result = await resultTask; // Wait for ack to arrive or timeout
                    
                    if (!result) Console.WriteLine("ack did not arrive");
                } while (!result && retry && !ct.IsCancellationRequested); // Loop this until success if the retry flag is set (i.e. we are on the client)

                InProgress.TryGetValue(message, out var tcs); // Get the TCS associated with the message
                if (tcs is null) {
                    Console.WriteLine("Cannot complete ack wait, tcs is null");
                    continue;
                }
                tcs.TrySetResult(result); // Set the result (finish the waiting in EnqueueMessage)
            }
        }
        catch(OperationCanceledException) {
            Console.WriteLine("Ack waiting has cancelled");
        }

        // Cleanup when ct signals
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

    // Submit ack to the tracker
    public void SubmitAck(MessageData message) {
        tracker.Complete((message.Id, message.TargetId));
    }


}