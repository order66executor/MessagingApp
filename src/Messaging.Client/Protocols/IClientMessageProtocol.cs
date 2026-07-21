namespace Messaging.Client.Protocols;

using Messaging.Shared;
using Messaging.Shared.Protocol;

public interface IClientMessageProtocol : IMessageProtocol {
    MessageData CreateIntroduction();
    Task SendTextMessageAsync(StringIdentifier target, string text);
}