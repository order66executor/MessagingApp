using System.Collections.Concurrent;

using Messaging.Server.Services;
using Messaging.Shared.Models;

namespace Messaging.Server.Protocols;

public class ServerProtocolFactory : IServerMessageProtocolFactory {
    public IServerMessageProtocol CreateProtocol(int startingId, ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, MessageRouter router) {
        return new ServerProtocol(handlers, router);
    }
}