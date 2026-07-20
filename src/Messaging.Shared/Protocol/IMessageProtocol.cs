using System.Net.Sockets;
using Messaging.Shared;

namespace Messaging.Shared.Protocol;

public interface IMessageProtocol {

    Task<bool> ProcessAsync(MessageData message);

    MessageData CreateAck(int id, StringIdentifier source, StringIdentifier target, int idToAck);

}