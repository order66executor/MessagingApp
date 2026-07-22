using Messaging.Shared.Models;

namespace Messaging.UI.Messages;

public class NewMessageReceivedMessage
{
    public MessageWrapper Wrapper { get; }
    public NewMessageReceivedMessage(MessageWrapper wrapper) => Wrapper = wrapper;
}
