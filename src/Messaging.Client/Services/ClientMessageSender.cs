namespace Messaging.Client.Services;

using Messaging.Shared.Models;
using MessagePack;

using System.Text;
using Messaging.Shared.Services;
using System.Collections.Concurrent;
using System.Security.Cryptography;

public class ClientMessageSender {
    private readonly StringIdentifier identifier;
    private readonly ClientDbHandler dbHandler;
    private readonly AckWaitHandler ackHandler;
    private readonly MessageConnectionHandler connHandler;
    private readonly IFileStorageService storageService;
    private readonly ConcurrentDictionary<string, string> pendingFiles;

    public ClientMessageSender(StringIdentifier identifier, ClientDbHandler dbHandler,
     AckWaitHandler ackHandler, MessageConnectionHandler connHandler, IFileStorageService storageService,
     ConcurrentDictionary<string, string> pendingFiles) {
        this.identifier = identifier;
        this.dbHandler = dbHandler;
        this.ackHandler = ackHandler;
        this.connHandler = connHandler;
        this.storageService = storageService;
        this.pendingFiles = pendingFiles;
    }

    public MessageData CreateAccountMessage(string password, MessageType type) {
        return type is not (MessageType.Login or MessageType.Register)
            ? throw new ArgumentException("Type is incorrect for account-related messages")
            : new() {
            Id = 0,
            Type = type,
            SourceId = identifier,
            TargetId = new StringIdentifier("SYSTEM"),
            SentAtUtc = DateTime.UtcNow,
            Payload = Encoding.UTF8.GetBytes(password)
        };
    }


    private async Task<MessageData> CreateMessageDataAsync(MessageType type, StringIdentifier target, byte[] payload) {
        MessageData message = new() {
            Id = target == StringIdentifier.System ? 0 : await dbHandler.GetHighestSequenceIdAsync(target) + 1,
            Type = type,
            SourceId = identifier,
            TargetId = target,
            SentAtUtc = DateTime.UtcNow,
            Payload = payload
        };


        return message;
        
    }

    // Sends the message and waits for an ack before returning, optionally saves the message to the db (turn off when sending system messages)
    private async Task SendAndWaitForAckAsync(MessageData message, bool saveToDb) {
        MessageData messageToSave = message;

        // Strip the heavy binary data before saving to local SQLite DB
        /* if (message.Type == MessageType.FileUpload) {
            var originalPayload = MessagePackSerializer.Deserialize<FileUploadPayload>(message.Payload);
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
                    Payload = MessagePackSerializer.Serialize(emptyPayload)
                };
            }
        } */
        MessageWrapper? wrapper = null;
        if (saveToDb) 
            wrapper = await dbHandler.PlaceMessageAsync(messageToSave, MessageState.Pending);
        bool result = await ackHandler.EnqueueMessageAsync(message); // Send the ORIGINAL message with full data and wait for ack

        if (result) Console.WriteLine($"Ack arrived for {message.Id} to {message.TargetId}"); 


        // Save wrapped message to db
        if (saveToDb && wrapper is not null) 
            await dbHandler.UpdateMessageStateAsync(wrapper.Id, result ? MessageState.Sent : MessageState.Unsent);

    }

    public async Task SendTextMessageAsync(StringIdentifier target, string text) {
        MessageData message = await CreateMessageDataAsync(MessageType.TextMessage, target, Encoding.UTF8.GetBytes(text));
        await SendAndWaitForAckAsync(message, saveToDb: true);
    }

    public async Task SendFileUploadAsync(StringIdentifier target, string filePath) {
        var hash = await storageService.GetSha256Async(filePath);
        pendingFiles.TryAdd(hash, filePath);
        var payload = new FileUploadPayload { FileName = Path.GetFileName(filePath), FileSize = storageService.GetFileSize(filePath), Sha256Hash = hash };

        MessageData message = await CreateMessageDataAsync(MessageType.FileUpload, target, MessagePackSerializer.Serialize(payload));
        await SendAndWaitForAckAsync(message, saveToDb: true);
    }

    public async Task RequestFileAsync(string fileId) {
        var payload = new FileRequestPayload { FileId = fileId };
        // The server needs a way to know who is requesting, so target is SYSTEM, and source is this client
        MessageData message = await CreateMessageDataAsync(MessageType.FileRequest, new StringIdentifier("SYSTEM"), MessagePackSerializer.Serialize(payload));
        await SendAndWaitForAckAsync(message, saveToDb: false);
    }

    public async Task SendSegmentAsync(Segment segment) {
        var payload = MessagePackSerializer.Serialize(segment);

        MessageData message = await CreateMessageDataAsync(MessageType.Segment, StringIdentifier.System, payload);
        await connHandler.WriteToOutBufferAsync(message);
    }


}
