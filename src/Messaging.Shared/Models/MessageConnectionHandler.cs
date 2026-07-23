using Messaging.Shared.Protocol;

namespace Messaging.Shared.Models;

public class MessageConnectionHandler {

    private readonly MessageConnection conn;

    //buffer to write outgoing messages to
    private readonly MessageDataBuffer outBuffer;
    private readonly CancellationTokenSource ct;


    public MessageConnectionHandler(MessageConnection conn, CancellationTokenSource cts) {
        this.conn = conn;
        ct = cts;
        outBuffer = new();
    }

    // Starts processing messages and handles exiting
    public async Task StartProcessingAsync(IMessageProtocol protocol) {
        Task incomingTask = ProcessIncomingAsync(protocol);
        Task outgoingTask = ProcessOutgoingAsync();

        await Task.WhenAny(incomingTask, outgoingTask);
        ct.Cancel();
        await Task.WhenAll(incomingTask, outgoingTask);
        outBuffer.Dispose();
    }

    // Asynchronously read forever from conn.Buffer for incoming messages and pass them to protocol
    private async Task ProcessIncomingAsync(IMessageProtocol protocol) {
        try {
            // wait forever for incoming
            await foreach (MessageData data in conn.Buffer.Reader.ReadAllAsync(ct.Token)) {

                // pass to protocol for handling
                if (await protocol.ProcessAsync(data)) {

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

    // Async wait for messages to be written to outBuffer and write them to the stream
    private async Task ProcessOutgoingAsync() {
        try {
            await foreach (MessageData data in outBuffer.Reader.ReadAllAsync(ct.Token)) {
                await conn.WriteAsync(data);
            }
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Outgoing Operation cancelled");
        }

    }

    public async Task WriteToOutBufferAsync(MessageData message) {
        await outBuffer.Writer.WriteAsync(message, ct.Token);
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