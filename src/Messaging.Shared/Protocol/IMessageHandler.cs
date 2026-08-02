using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;

public interface IMessageHandler {
    MessageType SupportedType { get; }

    public Task<bool> HandleAsync(MessageData message);
}