using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

using MessagePack;
using Messaging.Shared.Services;
using System.Collections.Concurrent;
using Messaging.Client.Services;

namespace Messaging.Client.Protocols.Handlers;

public class FileTransferReadyHandler : IMessageHandler {
    public MessageType SupportedType { get; } = MessageType.FileTransferReady;

    private readonly MessageConnectionHandler connHandler;
    private readonly IFileStorageService storageService;
    private readonly ConcurrentDictionary<Guid, string> currentUploads;
    private readonly ClientMessageSender sender;

    public FileTransferReadyHandler(MessageConnectionHandler connHandler, 
        IFileStorageService storageService, ConcurrentDictionary<Guid, string> currentUploads,
        ClientMessageSender sender) {
        this.connHandler = connHandler;
        this.storageService = storageService;
        this.currentUploads = currentUploads;
        this.sender = sender;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        _ = Process(message);

        return true;
    }

    private async Task<bool> Process(MessageData message) {
        // Deserialize payload
        var readyPayload = MessagePackSerializer.Deserialize<FileTransferReadyPayload>(message.Payload);

        if (readyPayload != null) {
            // Segmentize
            if (!currentUploads.TryRemove(readyPayload.ClientTransferId, out string? path)) Console.WriteLine("File is not pending");
            if (path is null) {
                Console.WriteLine("Path is null");
                return false;
            }

            Segment segment;

            await foreach(var data in storageService.ReadAllAsync(path)) {
                segment = new() {
                    Id = readyPayload.FileId,
                    Size = data.Length,
                    Data = data.ToArray(),
                    IsEnd = false
                };

                await sender.SendSegmentAsync(segment);

            }

            segment = new() {
                Id = readyPayload.FileId,
                Size = 0,
                Data = [ ],
                IsEnd = true
            };

            await sender.SendSegmentAsync(segment);

            Console.WriteLine("File sent");
        }

        return true;
    }


}