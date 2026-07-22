using Messaging.Shared.Models;
namespace Messaging.Shared.Models;

public record MessageData {

    // this ID should be sequential for every message the user sends in a conversation, starts from 1
    public required long Id { get; set; }
    public required MessageType Type { get; set; }
    public required StringIdentifier SourceId { get; set; }
    public required StringIdentifier TargetId { get; set; }
    public required DateTimeOffset SentAtUtc { get; set; }
    public required byte[] Payload { get; set; }
}
