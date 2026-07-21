using Messaging.Client.Services;
using Messaging.Shared;

namespace Messaging.Client.Protocols;

public class ClientProtocolFactory : IClientMessageProtocolFactory {
    public IClientMessageProtocol CreateProtocol(StringIdentifier identifier, MessageConnectionHandler handler, ClientDbHandler dbHandler) {
        return new ClientProtocol(identifier, handler, dbHandler);
    }
}
        