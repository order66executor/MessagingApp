using System.Net.Sockets;
using Messaging.Shared;

namespace Messaging.Shared.Protocols;

public interface IMessageProtocol {

    Task<bool> ProcessAsync(int id, MessageData data, MessageDataBuffer outBuf); 

    MessageData CreateIntroduction(StringIdentifier identifier);

    StringIdentifier ReceiveIntroduction(MessageData message);

    MessageData CreateAck(int id, StringIdentifier source, StringIdentifier target, int idToAck);

}