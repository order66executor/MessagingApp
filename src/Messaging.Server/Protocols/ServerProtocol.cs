using Messaging.Shared.Models;
using Messaging.Shared.Protocol;
namespace Messaging.Server.Protocols;

public class ServerDispatcher : MessageDispatcher {
    public ServerDispatcher(IEnumerable<IMessageHandler> handlers) : base(handlers) {}
    public sealed override async Task<bool> ProcessAsync(StringIdentifier identifier, MessageData message) {
        return identifier == message.SourceId && await base.ProcessAsync(identifier, message);
    }

}