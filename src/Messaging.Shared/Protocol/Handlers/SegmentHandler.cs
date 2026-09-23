using System.Collections.Concurrent;

using MessagePack;

using Messaging.Shared.Models;
using Messaging.Shared.Services;

namespace Messaging.Shared.Protocol.Handlers;

public class SegmentHandler : IMessageHandler {
    public MessageType SupportedType { get; } = MessageType.Segment;

    private readonly IFileStorageService storageService;
    private readonly ConcurrentDictionary<Guid, string> hashes;


    public SegmentHandler(IFileStorageService storageService, ConcurrentDictionary<Guid, string> hashes) {
        this.storageService = storageService;
        this.hashes = hashes;
    }

    public async Task<bool> HandleAsync(MessageData message) {
        Segment segment = MessagePackSerializer.Deserialize<Segment>(message.Payload);
        Guid guid = segment.Id;

        bool success = await storageService.WriteAsync(guid, segment.Data);
        if (!success) {
            Console.WriteLine("Failed to write segment to disk");
            return false;
        }
        bool match = false;

        if (segment.IsEnd) {
            if (!hashes.TryGetValue(guid, out var hash)) {
                Console.WriteLine("Hash is not in dict");
                return false;
            }

            match = await storageService.CheckSha256Async(storageService.GetPathOfStream(guid), hash);

            if (match) Console.WriteLine("Hashes match");
            else Console.WriteLine("Hashes do NOT match!");

            storageService.CloseStream(guid); 
        }

        return match;
    }
}