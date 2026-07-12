using Messaging.Shared;
namespace Messaging.Shared;

public record MessageData {

    // this ID should be unique for the connection
    public required int Id;
    public required MessageType Type;
    public required StringIdentifier SourceId;
    public required StringIdentifier TargetId;
    public required DateTimeOffset SentAtUtc;
    public required byte[] Payload;
}
