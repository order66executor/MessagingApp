using System.Net.Sockets;
using Messaging.Shared;

namespace Messaging.Shared.Protocols;

public interface IMessageProtocol {

    Task ProcessAsync(MessageData data);

    Task IntroduceAsync(MessageConnection conn, int id, StringIdentifier identifier);

    StringIdentifier ReceiveIntroduction(MessageConnection conn);

    Task SendAckAsync(MessageConnection conn, int id, StringIdentifier source, StringIdentifier target, int idToAck);

}