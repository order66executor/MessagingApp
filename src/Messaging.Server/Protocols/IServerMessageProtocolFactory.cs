using System.Collections.Concurrent;

using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Server.Protocols;

public interface IServerMessageProtocolFactory {
    IServerMessageProtocol CreateProtocol(IEnumerable<IMessageHandler> handlers);
}