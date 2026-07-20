using System.Buffers.Binary;

namespace Messaging.Shared.Protocol;


public abstract class ProtocolBase : IMessageProtocol {

    public abstract Task<bool> ProcessAsync(MessageData message);

    protected async Task EnqueueAck(int id, MessageConnectionHandler handler, StringIdentifier source, StringIdentifier target, int idToAck) {
        await handler.WriteToOutBufferAsync(CreateAck(id, source, target, idToAck));
    }

    public virtual MessageData CreateAck(int id, StringIdentifier source, StringIdentifier target, int idToAck) {
        byte[] idAsBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(idAsBytes, idToAck);
        MessageData message = new() {
            Id = id,
            Type = MessageType.Ack,
            SourceId = source,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = idAsBytes
        };

        return message;
    }
}