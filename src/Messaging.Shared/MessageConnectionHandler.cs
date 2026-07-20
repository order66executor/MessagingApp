using Messaging.Shared.Protocol;

namespace Messaging.Shared;

public class MessageConnectionHandler {

    private readonly MessageConnection conn;
    private readonly MessageDataBuffer outBuffer;
    private readonly CancellationTokenSource ct;


    public MessageConnectionHandler(MessageConnection conn, CancellationToken ct) {
        this.conn = conn;
        this.ct = CancellationTokenSource.CreateLinkedTokenSource(ct);
        this.outBuffer = new();
    }

    public async Task StartProcessingAsync(IMessageProtocol protocol) {
        Task incomingTask = ProcessIncomingAsync(protocol);
        Task outgoingTask = ProcessOutgoingAsync();

        await Task.WhenAny(incomingTask, outgoingTask);
        ct.Cancel();
        await Task.WhenAll(incomingTask, outgoingTask);
        outBuffer.Dispose();
    }

    private async Task ProcessIncomingAsync(IMessageProtocol protocol) {
        try {
            await foreach (MessageData data in conn.Buffer.Reader.ReadAllAsync(ct.Token)) {
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

    public async Task<bool> WaitForIncomingAsync(CancellationToken ct) {
        return await conn.Buffer.Reader.WaitToReadAsync(ct);
    }

    public async Task<MessageData> ReadOneIncomingAsync(CancellationToken ct) {
        return await conn.Buffer.Reader.ReadAsync(ct);
    }
}