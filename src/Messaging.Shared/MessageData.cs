using Messaging.Shared;
namespace Messaging.Shared;

public record MessageData {

    // this ID should be unique for the connection
    public required int Id { get; set; }
    public required MessageType Type { get; set; }
    public required StringIdentifier SourceId { get; set; }
    public required StringIdentifier TargetId { get; set; }
    public required DateTimeOffset SentAtUtc { get; set; }
    public required byte[] Payload { get; set; }
}
