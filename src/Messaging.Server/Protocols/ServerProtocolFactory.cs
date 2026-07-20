using System.Collections.Concurrent;

using Messaging.Shared;

namespace Messaging.Server.Protocols;

public class ServerProtocolFactory : IServerMessageProtocolFactory {
    public IServerMessageProtocol CreateProtocol(int startingId, ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers) {
        return new ServerProtocol(handlers);
    }
}