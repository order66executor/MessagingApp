

using System.Buffers.Binary;
using System.Text;

namespace Messaging.Shared.Protocols;

public class StandardProtocol : IMessageProtocol {

    public async Task ProcessAsync(MessageData data) {
        if (data.Type == MessageType.Ack) Console.WriteLine("Ack received");
    }
    public async Task IntroduceAsync(MessageConnection conn, int id, StringIdentifier identifier) {
        MessageData message = new() {
            Id = id,
            Type = MessageType.Introduction,
            SourceId = identifier,
            TargetId = new StringIdentifier(null),
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes("Hello")
        };

        await conn.WriteAsync(message);
    }

    public async Task<StringIdentifier> ReceiveIntroductionAsync(MessageConnection conn) {
        conn.Buffer.TryDequeue(out MessageData message);
        return message.SourceId;
    }
    public async Task SendAckAsync(MessageConnection conn, int id, StringIdentifier source, StringIdentifier target, int idToAck) {
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

        await conn.WriteAsync(message);
    }
}