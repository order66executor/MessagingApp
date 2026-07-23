using Messaging.Client.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Services;

namespace Messaging.Client.Protocols;

public interface IClientMessageProtocolFactory {
    IClientMessageProtocol CreateProtocol(StringIdentifier identifier, MessageConnectionHandler handler, ClientDbHandler dbHandler, AckWaitHandler ackHandler);
}