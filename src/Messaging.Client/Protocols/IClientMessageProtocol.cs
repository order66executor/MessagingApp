namespace Messaging.Client.Protocols;

using Messaging.Shared;
using Messaging.Shared.Protocol;

public interface IClientMessageProtocol : IMessageProtocol {
    MessageData CreateIntroduction();
    MessageData CreateTextMessage(StringIdentifier target, string text);
}