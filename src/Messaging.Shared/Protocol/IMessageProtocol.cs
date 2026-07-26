using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;

public interface IMessageProtocol {

    Task<bool> ProcessAsync(StringIdentifier sourceId, MessageData message);

    MessageData CreateAck(StringIdentifier source, StringIdentifier target, long idToAck);

    MessageData CreateNack(StringIdentifier source, StringIdentifier target, long idToNack, string reason);

}