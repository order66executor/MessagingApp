using Messaging.Shared.Models;
using Messaging.Shared.Protocol;
namespace Messaging.Server.Protocols;


// Dispatcher class for the server that handles incoming messages and checks for ID mismatches between the message data and the connection's owner
public class ServerDispatcher : MessageDispatcher {
    public ServerDispatcher(IEnumerable<IMessageHandler> handlers) : base(handlers) {}
    public sealed override async Task<bool> ProcessAsync(StringIdentifier identifier, MessageData message) {
        return identifier == message.SourceId && await base.ProcessAsync(identifier, message);
    }

}