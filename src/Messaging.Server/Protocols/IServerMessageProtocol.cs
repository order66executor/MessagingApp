namespace Messaging.Server.Protocols;

using Messaging.Shared;
using Messaging.Shared.Protocol;

public interface IServerMessageProtocol : IMessageProtocol {
    StringIdentifier ReceiveIntroduction(MessageData message);
}