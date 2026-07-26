using System.Text;

using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;


public abstract class ProtocolBase : IMessageProtocol {

    public abstract Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message);

    protected async Task EnqueueAck(MessageConnectionHandler handler, StringIdentifier source, StringIdentifier target, long idToAck) {
        await handler.WriteToOutBufferAsync(CreateAck(source, target, idToAck));
    }

    public virtual MessageData CreateAck(StringIdentifier source, StringIdentifier target, long idToAck) {
        MessageData message = new() {
            Id = idToAck,
            Type = MessageType.Ack,
            SourceId = source,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = [ ]
        };

        return message;
    }

    public virtual MessageData CreateNack(StringIdentifier source, StringIdentifier target, long idToNack, string reason = "") {
        return new() {
            Id = idToNack,
            Type = MessageType.Nack,
            SourceId = source,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes(reason)
        };
    }
}