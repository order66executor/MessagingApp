namespace Messaging.Client.Protocols;

using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

public interface IClientMessageProtocol : IMessageProtocol {
    MessageData CreateAccountMessage(string password, MessageType type);
    Task SendTextMessageAsync(StringIdentifier target, string text);
    Task SendFileAsync(StringIdentifier target, string filePath);
    Task RequestFileAsync(string fileId);
}