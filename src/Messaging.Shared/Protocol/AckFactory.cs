using System.Text;

using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;

public static class AckFactory {
    public static MessageData CreateAck(StringIdentifier source, StringIdentifier target, long idToAck) {
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

    public static MessageData CreateNack(StringIdentifier source, StringIdentifier target, long idToNack, string reason = "") {
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