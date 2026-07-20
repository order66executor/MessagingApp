using Messaging.Shared;

namespace Messaging.Client.Protocols;

public class ClientProtocolFactory : IClientMessageProtocolFactory {
    public IClientMessageProtocol CreateProtocol(int startingId, StringIdentifier identifier, MessageConnectionHandler handler) {
        return new ClientProtocol(startingId, identifier, handler);
    }
}
        