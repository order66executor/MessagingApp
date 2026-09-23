using System.Collections.Concurrent;

using MessagePack;

using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;
using Messaging.Shared.Services;
namespace Messaging.Server.Protocols.Handlers;

public class FileUploadHandler : IMessageHandler {
    public MessageType SupportedType => MessageType.FileUpload;

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly MessageRouter router;
    private readonly IFileStorageService storageService;
    private readonly ConcurrentDictionary<Guid, string> hashes;

    public FileUploadHandler(
        ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers,
        MessageRouter router,
        IFileStorageService storageService,
        ConcurrentDictionary<Guid, string> hashes) {

        this.handlers = handlers;
        this.router = router;
        this.storageService = storageService;
        this.hashes = hashes;
    }


    public async Task<bool> HandleAsync(MessageData message) {
        Guid fileId = Guid.NewGuid();
        if (!handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler)) return false;


        var uploadPayload = MessagePackSerializer.Deserialize<FileUploadPayload>(message.Payload);
        if (uploadPayload is null) return false;

        // Open stream to storage
        string sanitizedFileName = Path.GetFileName(uploadPayload.FileName);
        string localFileName = $"{fileId}_{sanitizedFileName}";
        storageService.CreateWriteStream(fileId, localFileName);

        // Acknowledge receipt
        var ack = AckFactory.CreateAck(StringIdentifier.System, message.TargetId, message.Id);
        await handler.WriteToOutBufferAsync(ack);

        hashes.TryAdd(fileId, uploadPayload.Sha256Hash);

        // Signal ready
        var readyPayload = new FileTransferReadyPayload() {
            FileId = fileId.ToString(),
            Sha256Hash = uploadPayload.Sha256Hash
        };
        MessageData readyMessage = new() {
            Id = 0,
            Type = MessageType.FileTransferReady,
            SourceId = StringIdentifier.System,
            TargetId = message.SourceId,
            SentAtUtc = DateTime.UtcNow,
            Payload = MessagePackSerializer.Serialize(readyPayload)
        };
        await handler.WriteToOutBufferAsync(readyMessage);

        


        // Notify recipient
        var notificationPayload = new FileNotificationPayload() {
            FileId = fileId.ToString(),
            FileName = Path.GetFileName(uploadPayload.FileName),
            FileSize = uploadPayload.FileSize
        };

        MessageData notificationMessage = new() {
            Id = message.Id,
            Type = MessageType.FileNotification,
            SourceId = message.SourceId,
            TargetId = message.TargetId,
            SentAtUtc = DateTime.UtcNow,
            Payload = MessagePackSerializer.Serialize(notificationPayload)
        };

        _ = router.RouteMessageAsync(notificationMessage);
        return true;
    }

}



