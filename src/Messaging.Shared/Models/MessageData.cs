using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
public class MessageData {

    // this ID should be sequential for every message the user sends in a conversation, starts from 1
    [Key(0)]
    public required long Id { get; set; }
    [Key(1)]
    public required MessageType Type { get; set; }
    [Key(2)]
    public required StringIdentifier SourceId { get; set; }
    [Key(3)]
    public required StringIdentifier TargetId { get; set; }
    [Key(4)]
    public required DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    [Key(5)]
    public required byte[] Payload { get; set; }
}
