using Messaging.Shared;
using Messaging.Shared.Protocol;

using System.Text;

namespace Messaging.Client.Protocols;

public class ClientProtocol : ProtocolBase, IClientMessageProtocol {

    private readonly MessageConnectionHandler handler;

    private readonly StringIdentifier identifier;

    public ClientProtocol(int startingId, StringIdentifier identifier, MessageConnectionHandler handler) : base(startingId) {
        this.handler = handler;
        this.identifier = identifier;
    }

    public sealed override async Task<bool> ProcessAsync(MessageData message) {
        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK Received");
                return true;

            case MessageType.TextMessage:
                Console.WriteLine($"Message received from: {message.SourceId}, content: {Encoding.UTF8.GetString(message.Payload)}");
                await EnqueueAck(handler, identifier, new StringIdentifier("SYSTEM"), message.Id);
                return true;

            default:
                return false;
            
        }
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
}

