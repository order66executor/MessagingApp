using Messaging.Client.Services;
using Messaging.Shared.Models;

namespace Messaging.Client.Protocols;

public interface IClientMessageProtocolFactory {
    IClientMessageProtocol CreateProtocol(StringIdentifier identifier, MessageConnectionHandler handler, ClientDbHandler dbHandler);
}