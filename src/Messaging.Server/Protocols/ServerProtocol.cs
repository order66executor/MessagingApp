using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

using System.Collections.Concurrent;
using System.Text;
using Messaging.Server.Services;

namespace Messaging.Server.Protocols;

public class ServerProtocol : ProtocolBase, IServerMessageProtocol {

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;

    private readonly MessageRouter router;

    public ServerProtocol(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, MessageRouter router) {
        this.handlers = handlers;
        this.router = router;
    }

    public StringIdentifier ReceiveIntroduction(MessageData message) => message.SourceId;

    public sealed override async Task<bool> ProcessAsync(MessageData message) {
        bool result;

        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK received");
                router.AckHandler.SubmitAck(message);
                return true;
            
            case MessageType.TextMessage:
                Console.WriteLine($"Text message with ID: {message.Id} received from: {message.SourceId} to: {message.TargetId} sent at: {message.SentAtUtc} content: {Encoding.UTF8.GetString(message.Payload)}");
                if (handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler)) {
                    try {
                        // await EnqueueAck(handler, message.SourceId, message.TargetId, message.Id);

                    }
                    catch (Exception e) {
                        Console.WriteLine($"Exception thrown while replying Ack to sender: {e.Message}");
                    }
                }
                else {
                    Console.WriteLine("Sender disconnected before Ack could be sent");
                }

                result = await router.UpdateHighestAckAsync(message);

                if (result)
                    _ = router.RouteMessageAsync(message);
                else Console.WriteLine("Message already acked, discarding");
                return true;
            
            default:
                return false;
        }
    }


}