using System.Net.Sockets;
using Messaging.Shared.UserIdentifiers;

namespace Messaging.Shared.Protocols;

public interface IMessageProtocol {

    Task IntroduceAsync(MessageConnection conn, StringIdentifier identifier);

    Task<StringIdentifier> ReceiveIntroductionAsync(MessageConnection conn);

}