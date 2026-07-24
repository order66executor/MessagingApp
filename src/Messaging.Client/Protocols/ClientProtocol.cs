using Messaging.Client.Services;
using Messaging.Shared.Protocol;
using Messaging.Shared.Models;

using System.Text;
using System.Text.Json;
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
                var resPayload = JsonSerializer.Deserialize<FileResponsePayload>(message.Payload);
                if (resPayload != null) {
                    string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    string savePath = Path.Combine(downloadsDir, resPayload.FileName);
                    await File.WriteAllBytesAsync(savePath, resPayload.FileData);
                    Console.WriteLine($"File downloaded and saved to: {savePath}");
                    // Here we might want to trigger a local UI event
                }
                await EnqueueAck(connHandler, message.SourceId, message.TargetId, message.Id);
                return true;

            default:
                return false;
            
        }
    }

    public MessageData CreateIntroduction() {
        return new() {
            Id = 0,
            Type = MessageType.Introduction,
            SourceId = identifier,
            TargetId = new StringIdentifier("SYSTEM"),
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes("Hello")
        };

    }

    private async Task<MessageData> CreateMessageDataAsync(MessageType type, StringIdentifier target, byte[] payload) {
        MessageData message = new() {
            Id = await dbHandler.GetHighestSequenceIdAsync(target) + 1,
            Type = type,
            SourceId = identifier,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = payload
        };


        return message;
        
    }

    private async Task SendAndWaitForAckAsync(MessageData message) {
        MessageData messageToSave = message;

        // Strip the heavy binary data before saving to local SQLite DB
        if (message.Type == MessageType.FileUpload) {
            var originalPayload = JsonSerializer.Deserialize<FileUploadPayload>(message.Payload);
            if (originalPayload != null) {
                var emptyPayload = new FileUploadPayload { 
                    FileName = originalPayload.FileName, 
                    FileData = [] // Empty array to save space
                };
                messageToSave = new MessageData {
                    Id = message.Id,
                    Type = message.Type,
                    SourceId = message.SourceId,
                    TargetId = message.TargetId,
                    SentAtUtc = message.SentAtUtc,
                    Payload = JsonSerializer.SerializeToUtf8Bytes(emptyPayload)
                };
            }
        }

        var wrapper = await dbHandler.PlaceMessageAsync(messageToSave, MessageState.Pending);
        bool result = await ackHandler.EnqueueMessageAsync(message); // Send the ORIGINAL message with full data

        if (result) Console.WriteLine("Setting message state to sent");

        await dbHandler.UpdateMessageStateAsync(wrapper.Id, result ? MessageState.Sent : MessageState.Unsent);

    }

    public async Task SendTextMessageAsync(StringIdentifier target, string text) {
        MessageData message = await CreateMessageDataAsync(MessageType.TextMessage, target, Encoding.UTF8.GetBytes(text));
        await SendAndWaitForAckAsync(message);
    }

    public async Task SendFileAsync(StringIdentifier target, string filePath) {
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
        var payload = new FileUploadPayload { FileName = Path.GetFileName(filePath), FileData = fileBytes };
        MessageData message = await CreateMessageDataAsync(MessageType.FileUpload, target, JsonSerializer.SerializeToUtf8Bytes(payload));
        await SendAndWaitForAckAsync(message);
    }

    public async Task RequestFileAsync(string fileId) {
        var payload = new FileRequestPayload { FileId = fileId };
        // The server needs a way to know who is requesting, so target is SYSTEM, and source is this client
        MessageData message = await CreateMessageDataAsync(MessageType.FileRequest, new StringIdentifier("SYSTEM"), JsonSerializer.SerializeToUtf8Bytes(payload));
        await SendAndWaitForAckAsync(message);
    }

}

