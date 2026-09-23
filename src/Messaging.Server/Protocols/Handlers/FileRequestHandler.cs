using System.Collections.Concurrent;

using MessagePack;

using Messaging.Shared.Services;
using Messaging.Shared.Models;
using Messaging.Shared.Protocol;
using Messaging.Server.Services;

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

    // Routes the requested file to the source 
    public async Task<bool> HandleAsync(MessageData message) {
        _ = Process(message);
        return true;
        
    }

    private async Task<bool> Process(MessageData message) {
        Console.WriteLine("Received request");
        if (!handlers.TryGetValue(message.SourceId, out MessageConnectionHandler? handler))
            return false;

        // Acknowledge receipt (source and target has to be system, the ack logic sucks a little)
        var ack = AckFactory.CreateAck(StringIdentifier.System, StringIdentifier.System, message.Id);
        await handler.WriteToOutBufferAsync(ack);

        // Deserialize the request payload
        var requestPayload = MessagePackSerializer.Deserialize<FileRequestPayload>(message.Payload);
        if (requestPayload is null) return false;

        // Get the requested file from disk, currently loads the whole file into memory. TODO: file streaming and packetization
        string path = storageService.GetAbsolutePath($"{requestPayload.FileId}_*");
        string? filePath = Directory.GetFiles(path).FirstOrDefault();

        if (filePath is null) {
            Console.WriteLine("File ID not found");
            return false;
        }

        Console.WriteLine("File exists");

        // Construct file response payload
        var responsePayload = new FileResponsePayload() {
            FileId = requestPayload.FileId,
            FileName = Path.GetFileName(filePath)[(requestPayload.FileId.ToString().Length + 1)..],
            Sha256Hash = await storageService.GetSha256Async(filePath)
        };

        // Construct MessageData object with the payload
        MessageData response = new() {
            Id = message.Id,
            Type = MessageType.FileResponse,
            SourceId = new("SYSTEM"),
            TargetId = message.SourceId,
            SentAtUtc = DateTime.UtcNow,
            Payload = MessagePackSerializer.Serialize(responsePayload)
        };

        // Route the message and do await
        await router.RouteMessageAsync(response);
        Console.WriteLine("Response routed");

        Segment segment;
        MessageData segmentMessage = new() {
                Id = 0,
                Type = MessageType.Segment,
                SourceId = StringIdentifier.System,
                TargetId = message.SourceId,
                SentAtUtc = DateTime.UtcNow,
                Payload = [ ]
        };

        await foreach (var data in storageService.ReadAllAsync(filePath)) {
            segment = new() {
                Id = requestPayload.FileId,
                Size = data.Length,
                Data = data.ToArray(),
                IsEnd = false
            };

            segmentMessage.Payload = MessagePackSerializer.Serialize(segment);

            await handler.WriteToOutBufferAsync(segmentMessage);

        }

        segment = new() {
            Id = requestPayload.FileId,
            Size = 0,
            Data = [ ],
            IsEnd = true
        };
        segmentMessage.Payload = MessagePackSerializer.Serialize(segment);

        await handler.WriteToOutBufferAsync(segmentMessage);

        Console.WriteLine("File sent");

        return true;
    }
}