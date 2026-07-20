using Messaging.Shared;
using Messaging.Shared.Protocol;

using System.Text;

namespace Messaging.Client.Protocols;

public class ClientProtocol : ProtocolBase, IClientMessageProtocol {

    private readonly MessageConnectionHandler handler;

    private readonly StringIdentifier identifier;

    private int idCounter;

    public ClientProtocol(int startingId, StringIdentifier identifier, MessageConnectionHandler handler) {
        idCounter = startingId;
        this.handler = handler;
        this.identifier = identifier;
    }

    public sealed override async Task<bool> ProcessAsync(MessageData message) {
        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK Received");
                return true;

            case MessageType.TextMessage:
                Console.WriteLine($"Message ID: {message.Id} received from: {message.SourceId}, content: {Encoding.UTF8.GetString(message.Payload)}");
                await EnqueueAck(idCounter++, handler, identifier, new StringIdentifier("SYSTEM"), message.Id);
                return true;

            default:
                return false;
            
        }
    }

    public MessageData CreateIntroduction() {
        return new() {
            Id = idCounter++,
            Type = MessageType.Introduction,
            SourceId = identifier,
            TargetId = new StringIdentifier("SYSTEM"),
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes("Hello")
        };

    }

    public MessageData CreateTextMessage(StringIdentifier target, string text) {
        return new() {
            Id = idCounter++,
            Type = MessageType.TextMessage,
            SourceId = identifier,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes(text)
        };
    }

}

