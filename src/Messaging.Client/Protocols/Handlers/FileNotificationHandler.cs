using Messaging.Client.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Client.Protocols.Handlers;

// Handle file notification messages
public class FileNotificationHandler : IMessageHandler {
    public MessageType SupportedType { get; } = MessageType.FileNotification;
    private readonly MessageConnectionHandler connHandler;
    private readonly ClientDbHandler dbHandler;

    public FileNotificationHandler(ClientDbHandler dbHandler, MessageConnectionHandler connHandler) {
        this.connHandler = connHandler;
        this.dbHandler = dbHandler;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        Console.WriteLine($"File notification received from: {message.SourceId}");

        await dbHandler.PlaceMessageAsync(message, MessageState.Sent);
        await connHandler.WriteToOutBufferAsync(AckFactory.CreateAck(message.TargetId, message.SourceId, message.Id));

        return true;
    }

}


