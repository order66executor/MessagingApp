using System.Net.Sockets;
using Messaging.Shared;

namespace Messaging.Shared.Protocol;

public interface IMessageProtocol {

    Task<bool> ProcessAsync(MessageData message);

    MessageData CreateAck(StringIdentifier source, StringIdentifier target, int idToAck);

}