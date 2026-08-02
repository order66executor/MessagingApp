using System.Collections.Concurrent;

using MessagePack;

using Messaging.Server.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

namespace Messaging.Server.Protocols.Handlers;

public class FileRequestHandler : IMessageHandler {
    public MessageType SupportedType => MessageType.FileRequest;

    private readonly IFileStorageService storageService;
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly MessageRouter router;

    public FileRequestHandler(
        ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers,
        MessageRouter router,
        IFileStorageService storageService) {
        
        this.handlers = handlers;
        this.router = router;
        this.storageService = storageService;
    }


    public async Task<bool> HandleAsync(MessageData message) {
        if (!handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler))
            return false;

        // Acknowledge receipt
        var ack = AckFactory.CreateAck(new("SYSTEM"), new("SYSTEM"), message.Id);
        await handler.WriteToOutBufferAsync(ack);

        var requestPayload = MessagePackSerializer.Deserialize<FileRequestPayload>(message.Payload);
        if (requestPayload is null) return false;

        var requestedFile = await storageService.GetFileAsync(requestPayload.FileId);
        if (!requestedFile.HasValue) return false;

        var responsePayload = new FileResponsePayload() {
            FileId = requestPayload.FileId,
            FileName = requestedFile.Value.FileName,
            FileData = requestedFile.Value.Data
        };
        MessageData response = new() {
            Id = message.Id,
            Type = MessageType.FileResponse,
            SourceId = new("SYSTEM"),
            TargetId = message.SourceId,
            SentAtUtc = DateTime.UtcNow,
            Payload = MessagePackSerializer.Serialize(responsePayload)
        };

        _ = router.RouteMessageAsync(response);
        return true;
        
    }

}