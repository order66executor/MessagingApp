using Messaging.Shared.Protocol;

namespace Messaging.Shared.Models;

// Wrapper class for MessageConnection that listens for incoming messages on the connection object's buffer
// and forwards them for processing and listens for outgoing messages on the outBuffer 
// and forwards them to the conn object for writing to the stream
public class MessageConnectionHandler {

    private readonly MessageConnection conn;

    // Buffer to listen for outgoing messages in
    private readonly MessageDataBuffer outBuffer;
    private readonly CancellationTokenSource cts;
    public StringIdentifier UserId { get; set; }


    public MessageConnectionHandler(MessageConnection conn, CancellationToken ct) {
        this.conn = conn;
        cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        outBuffer = new();
    }

    // Starts processing messages and handles exiting
    public async Task StartProcessingAsync(IMessageDispatcher dispatcher) {
        Task incomingTask = ProcessIncomingAsync(dispatcher);
        Task outgoingTask = ProcessOutgoingAsync();

        await Task.WhenAny(incomingTask, outgoingTask);
        cts.Cancel();
        await Task.WhenAll(incomingTask, outgoingTask);
        outBuffer.Dispose();
    }

    // Asynchronously read forever from conn.Buffer for incoming messages and pass them to the dispatcher
    private async Task ProcessIncomingAsync(IMessageDispatcher dispatcher) {
        try {
            // wait forever for incoming
            await foreach (MessageData data in conn.Buffer.Reader.ReadAllAsync(cts.Token)) {

                // pass to protocol for handling
                if (await dispatcher.ProcessAsync(UserId, data)) {

                }
                else {
                    Console.WriteLine("Error processing message");
                }
            }
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Incoming Operation cancelled");
        }
    }

    // Async wait for messages to be written to outBuffer and call conn.WriteAsync
    private async Task ProcessOutgoingAsync() {
        try {
            await foreach (MessageData data in outBuffer.Reader.ReadAllAsync(cts.Token)) {
                await conn.WriteAsync(data);
            }
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Outgoing Operation cancelled");
        }

    }

    public async Task WriteToOutBufferAsync(MessageData message) {
        await outBuffer.Writer.WriteAsync(message, cts.Token);
    }

    // Returns if there is an incoming message to be read
    public async Task<bool> WaitForIncomingAsync(CancellationToken ct) {
        return await conn.Buffer.Reader.WaitToReadAsync(ct);
    }

    // Reads exactly one incoming message
    public async Task<MessageData> ReadOneIncomingAsync(CancellationToken ct) {
        return await conn.Buffer.Reader.ReadAsync(ct);
    }
}