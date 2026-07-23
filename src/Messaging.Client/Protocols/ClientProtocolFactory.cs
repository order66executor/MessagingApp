using Messaging.Client.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Services;

namespace Messaging.Client.Protocols;

public class ClientProtocolFactory : IClientMessageProtocolFactory {
    public IClientMessageProtocol CreateProtocol(StringIdentifier identifier, MessageConnectionHandler handler, ClientDbHandler dbHandler, AckWaitHandler ackhandler) {
        return new ClientProtocol(identifier, handler, dbHandler, ackhandler);
    }
}
        