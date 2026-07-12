using System.Net.Sockets;

using Messaging.Shared.UserIdentifiers;

namespace Messaging.Shared.Protocols;

public class StandardProtocol : IMessageProtocol {

    public Task IntroduceAsync(MessageConnection conn, StringIdentifier identifier) {

        
    }

    public Task<StringIdentifier> ReceiveIntroductionAsync(MessageConnection conn) {

    }


}