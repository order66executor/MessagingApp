using System.Collections.Concurrent;

using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Server.Protocols.Handlers;

public class TextMessageHandler : IMessageHandler {
    public MessageType SupportedType => MessageType.TextMessage;

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly MessageRouter router;

    public TextMessageHandler(
        ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers,
        MessageRouter router) {

        this.handlers = handlers;
        this.router = router;
    }


    public async Task<bool> HandleAsync(MessageData message) {
        if (!handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler))
            return false;

        // Acknowledge
        var ack = AckFactory.CreateAck(new("SYSTEM"), message.TargetId, message.Id);
        await handler.WriteToOutBufferAsync(ack);

        // Attempt to update the highest ack counter, if false the message is a duplicate
        if (await router.UpdateHighestAckAsync(message))

            // route message to recipient
            _ = router.RouteMessageAsync(message);
        else Console.WriteLine("Message already acked, discarding");

        return true;
    }


}
