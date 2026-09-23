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
    private readonly ConcurrentDictionary<Guid, string> currentUploads;
    private readonly ConcurrentDictionary<Guid, byte> currentDownloads;

    public ClientMessageSender(StringIdentifier identifier, ClientDbHandler dbHandler,
        AckWaitHandler ackHandler, MessageConnectionHandler connHandler, IFileStorageService storageService,
        ConcurrentDictionary<Guid, string> currentUploads,
        ConcurrentDictionary<Guid, byte> currentDownloads) {

        this.identifier = identifier;
        this.dbHandler = dbHandler;
        this.ackHandler = ackHandler;
        this.connHandler = connHandler;
        this.storageService = storageService;
        this.currentUploads = currentUploads;
        this.currentDownloads = currentDownloads;
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
    private async Task<bool> SendAndWaitForAckAsync(MessageData message, bool saveToDb) {
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

        return result;

    }

    public async Task SendTextMessageAsync(StringIdentifier target, string text) {
        MessageData message = await CreateMessageDataAsync(MessageType.TextMessage, target, Encoding.UTF8.GetBytes(text));

        await SendAndWaitForAckAsync(message, saveToDb: true);
    }

    public async Task SendFileUploadAsync(StringIdentifier target, string filePath) {
        Guid transferId = Guid.NewGuid();
        var hash = await storageService.GetSha256Async(filePath);

        currentUploads.TryAdd(transferId, filePath);

        var payload = new FileUploadPayload { FileName = Path.GetFileName(filePath), FileSize = storageService.GetFileSize(filePath), Sha256Hash = hash, ClientTransferId = transferId };

        MessageData message = await CreateMessageDataAsync(MessageType.FileUpload, target, MessagePackSerializer.Serialize(payload));
        await SendAndWaitForAckAsync(message, saveToDb: true);
    }

    public async Task RequestFileAsync(string fileId) {
        Guid guid = Guid.Parse(fileId);
        var payload = new FileRequestPayload { FileId = guid };

        if (!currentDownloads.TryAdd(guid, default)) {
            Console.WriteLine("The file is already being downloaded");
            return;
        }

        // The server needs a way to know who is requesting, so target is SYSTEM, and source is this client
        MessageData message = await CreateMessageDataAsync(MessageType.FileRequest, StringIdentifier.System, MessagePackSerializer.Serialize(payload));

        if (!await SendAndWaitForAckAsync(message, saveToDb: false))
            currentDownloads.Remove(guid, out _);
        Console.WriteLine("Request acked");
    }

    public async Task SendSegmentAsync(Segment segment) {
        var payload = MessagePackSerializer.Serialize(segment);

        MessageData message = await CreateMessageDataAsync(MessageType.Segment, StringIdentifier.System, payload);
        await connHandler.WriteToOutBufferAsync(message);
    }


}
