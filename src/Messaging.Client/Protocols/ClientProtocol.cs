using Messaging.Client.Services;
using Messaging.Shared.Protocol;
using Messaging.Shared.Models;

using System.Text;
using MessagePack;
using Messaging.Shared.Services;

namespace Messaging.Client.Protocols;

public class ClientProtocol : ProtocolBase, IClientMessageProtocol {

    private readonly MessageConnectionHandler connHandler;

    private readonly StringIdentifier identifier;

    private readonly ClientDbHandler dbHandler;
    private readonly AckWaitHandler ackHandler;

    public ClientProtocol(StringIdentifier identifier, MessageConnectionHandler connHandler, ClientDbHandler dbHandler, AckWaitHandler ackWaitHandler) {
        this.connHandler = connHandler;
        this.identifier = identifier;
        this.dbHandler = dbHandler;
        ackHandler = ackWaitHandler;
    }

    public sealed override async Task<bool> ProcessAsync(StringIdentifier id, MessageData message) {
        if (id.Value != "SYSTEM") return false;
        switch (message.Type) {
            case MessageType.Ack:
                Console.WriteLine("ACK Received");
                ackHandler.SubmitAck(message);
                return true;

            case MessageType.TextMessage:
                Console.WriteLine($"Message ID: {message.Id} received from: {message.SourceId}, content: {Encoding.UTF8.GetString(message.Payload)}");
                await dbHandler.PlaceMessageAsync(message, MessageState.Sent);
                await EnqueueAck(connHandler, message.TargetId, message.SourceId, message.Id);
                return true;

            case MessageType.FileNotification:
                Console.WriteLine($"File notification received from: {message.SourceId}");
                await dbHandler.PlaceMessageAsync(message, MessageState.Sent);
                await EnqueueAck(connHandler, message.TargetId, message.SourceId, message.Id);
                return true;

            case MessageType.FileResponse:
                var resPayload = MessagePackSerializer.Deserialize<FileResponsePayload>(message.Payload);
                if (resPayload != null) {
                    string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    string savePath = Path.Combine(downloadsDir, resPayload.FileName);
                    await File.WriteAllBytesAsync(savePath, resPayload.FileData);
                    Console.WriteLine($"File downloaded and saved to: {savePath}");
                    // Here we might want to trigger a local UI event
                }
                await EnqueueAck(connHandler, message.TargetId, message.SourceId, message.Id);
                return true;

            default:
                return false;
            
        }
    }
}

