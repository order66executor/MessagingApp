using System.Collections.Concurrent;

using MessagePack;

using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Server.Protocols.Handlers;

public class FileUploadHandler : IMessageHandler {
    public MessageType SupportedType => MessageType.FileUpload;

    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly MessageRouter router;
    private readonly IFileStorageService storageService;

    public FileUploadHandler(
        ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers,
        MessageRouter router,
        IFileStorageService storageService) {

        this.handlers = handlers;
        this.router = router;
        this.storageService = storageService;
    }


    public async Task<bool> HandleAsync(MessageData message) {
        if (!handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler)) return false;

        // Acknowledge receipt
        var ack = AckFactory.CreateAck(new("SYSTEM"), message.TargetId, message.Id);
        await handler.WriteToOutBufferAsync(ack);

        var uploadPayload = MessagePackSerializer.Deserialize<FileUploadPayload>(message.Payload);
        if (uploadPayload is null) return false;

        // Save to storage
        string fileId = await storageService.SaveFileAsync(uploadPayload.FileName, uploadPayload.FileData, CancellationToken.None);

        // Notify recipient
        var notificationPayload = new FileNotificationPayload() {
            FileId = fileId,
            FileName = Path.GetFileName(uploadPayload.FileName),
            FileSize = uploadPayload.FileData.Length
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



