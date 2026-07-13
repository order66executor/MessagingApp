using Messaging.Shared.Protocols;

namespace Messaging.Shared;

public class MessageConnectionHandler {

    private readonly IMessageProtocol protocol;
    private readonly MessageConnection conn;
    private readonly MessageDataBuffer outBuffer;
    private readonly CancellationTokenSource ct;

    private int outIdCounter;
    private readonly int idIncrement;

    public MessageConnectionHandler(IMessageProtocol protocol, MessageConnection conn, CancellationToken ct, int startingOutId, int idIncrement) {
        this.protocol = protocol;
        this.conn = conn;
        this.ct = CancellationTokenSource.CreateLinkedTokenSource(ct);
        this.outBuffer = new();
        outIdCounter = startingOutId;
        this.idIncrement = idIncrement;
    }

    public async Task StartProcessingAsync() {
        Task incomingTask = ProcessIncomingAsync();
        Task outgoingTask = ProcessOutgoingAsync();

        await Task.WhenAny(incomingTask, outgoingTask);
        ct.Cancel();
        await Task.WhenAll(incomingTask, outgoingTask);
        outBuffer.Dispose();
    }

    private async Task ProcessIncomingAsync() {
        try {
            await foreach (MessageData data in conn.Buffer.Reader.ReadAllAsync(ct.Token)) {
                if (await protocol.ProcessAsync(outIdCounter, data, outBuffer)) {
                    outIdCounter += idIncrement;
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
}