using Messaging.Shared.Models;
namespace Messaging.Shared.Protocol;

public class MessageDispatcher : IMessageDispatcher {
    protected readonly Dictionary<MessageType, IMessageHandler> handlers;

    public MessageDispatcher(IEnumerable<IMessageHandler> handlers) {
        this.handlers = handlers.ToDictionary(h => h.SupportedType);
    }
    public virtual async Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message) {
        // this works because && is short-circuit evaluation
        return handlers.TryGetValue(message.Type, out var handler) && await handler.HandleAsync(message);
    }


}