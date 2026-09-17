using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;

public interface IMessageDispatcher {
    Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message);
}