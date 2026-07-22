using System.Net.Sockets;
using Messaging.Shared.Models;

namespace Messaging.Shared.Protocol;

public interface IMessageProtocol {

    Task<bool> ProcessAsync(MessageData message);

    MessageData CreateAck(StringIdentifier source, StringIdentifier target, long idToAck);

}