using System.Collections.Concurrent;

using Messaging.Server.Services;
using Messaging.Shared;

namespace Messaging.Server.Protocols;

public interface IServerMessageProtocolFactory {
    IServerMessageProtocol CreateProtocol(int startingId, ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, MessageRouter router);
}