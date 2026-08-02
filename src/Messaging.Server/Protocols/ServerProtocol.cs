using Messaging.Shared.Models;
using Messaging.Shared.Protocol;
namespace Messaging.Server.Protocols;

public class ServerProtocol : ProtocolBase, IServerMessageProtocol {
    private readonly Dictionary<MessageType, IMessageHandler> handlers;

    public ServerProtocol(IEnumerable<IMessageHandler> handlers) {
        this.handlers = handlers.ToDictionary(h => h.SupportedType);
    }
    public sealed override async Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message) {
        // this works because && is short-circuit evaluation
        return sourceId == message.SourceId && handlers.TryGetValue(message.Type, out var handler) && await handler.HandleAsync(message);
    }


}