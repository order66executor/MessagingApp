using Messaging.Shared.Models;
using Messaging.Shared.Protocol;

using MessagePack;
using Messaging.Shared.Services;
using System.Collections.Concurrent;

namespace Messaging.Client.Protocols.Handlers;

public class FileResponseHandler : IMessageHandler {
    public MessageType SupportedType { get; } = MessageType.FileResponse;

    private readonly MessageConnectionHandler connHandler;
    private readonly IFileStorageService storageService;
    private readonly ConcurrentDictionary<Guid, string> hashes;

    public FileResponseHandler(MessageConnectionHandler connHandler, IFileStorageService storageService, ConcurrentDictionary<Guid, string> hashes) {
        this.connHandler = connHandler;
        this.storageService = storageService;
        this.hashes = hashes;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        // Deserialize payload
        var resPayload = MessagePackSerializer.Deserialize<FileResponsePayload>(message.Payload);
        Guid guid = resPayload.FileId;

        if (resPayload != null) {
            // Open stream
            storageService.CreateWriteStream(guid, resPayload.FileName);
            hashes.TryAdd(guid, resPayload.Sha256Hash);
        }

        // Reply ack
        await connHandler.WriteToOutBufferAsync(AckFactory.CreateAck(message.TargetId, message.SourceId, message.Id));
        return true;
    }


}

