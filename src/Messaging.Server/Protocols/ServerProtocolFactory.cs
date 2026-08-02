using System.Collections.Concurrent;

using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Server.Protocols;

public class ServerProtocolFactory : IServerMessageProtocolFactory {
    public IServerMessageProtocol CreateProtocol(IEnumerable<IMessageHandler> handlers) {
        return new ServerProtocol(handlers);
    }
}