using Messaging.Shared.Models;
using Messaging.Shared.Services;

namespace Messaging.Shared.Protocol.Handlers;

public class AckHandler : IMessageHandler {
    public MessageType SupportedType => MessageType.Ack;

    private readonly AckWaitHandler waitHandler;

    public AckHandler(AckWaitHandler waitHandler) {
        this.waitHandler = waitHandler;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        waitHandler.SubmitAck(message);
        return true;
    }

}