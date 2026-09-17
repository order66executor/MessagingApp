namespace Messaging.Client.Protocols.Handlers;

using System.Text;

using Messaging.Client.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

public class TextMessageHandler : IMessageHandler {   
    public MessageType SupportedType { get; } = MessageType.TextMessage;

    private readonly ClientDbHandler dbHandler;
    private readonly MessageConnectionHandler connHandler;

    public TextMessageHandler(ClientDbHandler dbHandler, MessageConnectionHandler connHandler) {
        this.dbHandler = dbHandler;
        this.connHandler = connHandler;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        Console.WriteLine($"Message ID: {message.Id} received from: {message.SourceId}, content: {Encoding.UTF8.GetString(message.Payload)}");
        await dbHandler.PlaceMessageAsync(message, MessageState.Sent);
        await connHandler.WriteToOutBufferAsync(AckFactory.CreateAck(message.TargetId, message.SourceId, message.Id));
        return true;

    }

}