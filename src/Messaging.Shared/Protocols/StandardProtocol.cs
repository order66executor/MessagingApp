

using System.Buffers.Binary;
using System.Text;
using System.Threading.Channels;

namespace Messaging.Shared.Protocols;

public class StandardProtocol : IMessageProtocol {
    public async Task<bool> ProcessAsync(int id, MessageData message, MessageDataBuffer outBuf) {
        if (message.Type == MessageType.Ack) Console.WriteLine("ACK RECV");
        return false;
    }
    public MessageData CreateIntroduction(StringIdentifier identifier) {
        return new() {
            Id = 0,
            Type = MessageType.Introduction,
            SourceId = identifier,
            TargetId = new StringIdentifier("SYSTEM"),
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes("Hello")
        };

    }

    public StringIdentifier ReceiveIntroduction(MessageData message) {
        return message.SourceId;
    }
    public MessageData CreateAck(int id, StringIdentifier source, StringIdentifier target, int idToAck) {
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