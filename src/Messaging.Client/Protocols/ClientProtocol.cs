using Messaging.Client.Services;
using Messaging.Shared.Protocol;
using Messaging.Shared.Models;

using System.Text;
using Messaging.Shared.Services;

namespace Messaging.Client.Protocols;

public class ClientProtocol : ProtocolBase, IClientMessageProtocol {

    private readonly MessageConnectionHandler connHandler;

    private readonly StringIdentifier identifier;

    private readonly ClientDbHandler dbHandler;
    private readonly AckWaitHandler ackHandler;

    public ClientProtocol(StringIdentifier identifier, MessageConnectionHandler connHandler, ClientDbHandler dbHandler, AckWaitHandler ackWaitHandler) {
        this.connHandler = connHandler;
        this.identifier = identifier;
        this.dbHandler = dbHandler;
        ackHandler = ackWaitHandler;
    }

    public sealed override async Task<bool> ProcessAsync(StringIdentifier id, MessageData message) {
        if (id.Value != "SYSTEM") return false;
        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK Received");
                ackHandler.SubmitAck(message);
                return true;

            case MessageType.TextMessage:
                Console.WriteLine($"Message ID: {message.Id} received from: {message.SourceId}, content: {Encoding.UTF8.GetString(message.Payload)}");
                await dbHandler.PlaceMessageAsync(message, MessageState.Sent);
                await EnqueueAck(connHandler, message.SourceId, message.TargetId, message.Id);
                return true;

            default:
                return false;
            
        }
    }

    public MessageData CreateIntroduction() {
        return new() {
            Id = 0,
            Type = MessageType.Introduction,
            SourceId = identifier,
            TargetId = new StringIdentifier("SYSTEM"),
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes("Hello")
        };

    }

    public async Task SendTextMessageAsync(StringIdentifier target, string text) {
        MessageData message = new() {
            Id = await dbHandler.GetHighestSequenceIdAsync(target) + 1,
            Type = MessageType.TextMessage,
            SourceId = identifier,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes(text)
        };

        var wrapper = await dbHandler.PlaceMessageAsync(message, MessageState.Pending);
        bool result = await ackHandler.EnqueueMessageAsync(message);

        if (result) Console.WriteLine("Setting message state to sent");

        await dbHandler.UpdateMessageStateAsync(wrapper.Id, result ? MessageState.Sent : MessageState.Unsent);
    }

}

