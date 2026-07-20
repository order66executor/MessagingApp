using Messaging.Shared;
using Messaging.Shared.Protocol;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text;

namespace Messaging.Server.Protocols;

public class ServerProtocol : ProtocolBase, IServerMessageProtocol {

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;

    private readonly ConcurrentDictionary<StringIdentifier, int> counters;

    public ServerProtocol(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers) {
        this.handlers = handlers;
        counters = new();
    }

    public StringIdentifier ReceiveIntroduction(MessageData message) => message.SourceId;

    public sealed override async Task<bool> ProcessAsync(MessageData message) {
        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK received");
                return true;
            
            case MessageType.TextMessage:
                Console.WriteLine($"Text message received from: {message.SourceId} to: {message.TargetId} sent at: {message.SentAtUtc} content: {Encoding.UTF8.GetString(message.Payload)}");
                if (handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler)) {
                    try {
                        await EnqueueAck(counters.GetOrAdd(message.SourceId, 1) ,handler, new StringIdentifier("SYSTEM"), message.SourceId, message.Id);
                        counters[message.SourceId]++;
                    }
                    catch (Exception e) {
                        Console.WriteLine($"Exception thrown while replying Ack to sender: {e.Message}");
                    }
                }
                else {
                    Console.WriteLine("Sender disconnected before Ack could be sent");
                }

                if (handlers.TryGetValue(message.TargetId, out handler)) {
                    message.Id = counters.AddOrUpdate(message.TargetId, 1, (_, value) => value + 1);
                    message.SentAtUtc = DateTime.UtcNow;

                    try {
                        await handler.WriteToOutBufferAsync(message);
                    }
                    catch (Exception e) {
                        Console.WriteLine($"Exception thrown while sending message to recipient: {e.Message}");
                        return false;
                    }
                }
                else {
                    Console.WriteLine("Target is not connected, message cannot be sent");
                    return false;
                }
                return true;
            
            default:
                return false;
        }
    }


}