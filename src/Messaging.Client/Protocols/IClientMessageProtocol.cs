namespace Messaging.Client.Protocols;

using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

public interface IClientMessageProtocol : IMessageProtocol {
    MessageData CreateIntroduction();
    Task SendTextMessageAsync(StringIdentifier target, string text);
}