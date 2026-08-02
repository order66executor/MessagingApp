using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Server.Protocols.Handlers;

public class AckHandler : IMessageHandler {
    public MessageType SupportedType => MessageType.Ack;

    private readonly MessageRouter router;

    public AckHandler(MessageRouter router) {
        this.router = router;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        router.AckHandler.SubmitAck(message);
        return true;
    }

}