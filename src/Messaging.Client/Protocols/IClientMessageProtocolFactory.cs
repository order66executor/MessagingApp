using Messaging.Shared;

namespace Messaging.Client.Protocols;

public interface IClientMessageProtocolFactory {
    IClientMessageProtocol CreateProtocol(int startingId, StringIdentifier identifier, MessageConnectionHandler handler);
}