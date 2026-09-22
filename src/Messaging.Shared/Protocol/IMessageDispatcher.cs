using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;

// Interface for dispatcher classes
public interface IMessageDispatcher {
    Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message);
}