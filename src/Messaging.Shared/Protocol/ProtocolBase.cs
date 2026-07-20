using System.Buffers.Binary;

namespace Messaging.Shared.Protocol;


public abstract class ProtocolBase : IMessageProtocol {

    protected readonly int idCounter;

    public ProtocolBase(int startingId) {
        idCounter = startingId;
    }

    public abstract Task<bool> ProcessAsync(MessageData message);

    protected async Task EnqueueAck(MessageConnectionHandler handler, StringIdentifier source, StringIdentifier target, int idToAck) {
        await handler.WriteToOutBufferAsync(CreateAck(source, target, idToAck));
    }

    public virtual MessageData CreateAck(StringIdentifier source, StringIdentifier target, int idToAck) {
        byte[] idAsBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(idAsBytes, idToAck);
        MessageData message = new() {
            Id = idCounter,
            Type = MessageType.Ack,
            SourceId = source,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = idAsBytes
        };

        return message;
    }
}