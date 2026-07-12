using Messaging.Shared.UserIdentifiers;
namespace Messaging.Shared;

public record MessageData {

    // this ID should be unique for the connection
    public required int Id;
    public required MessageType Type;
    public required StringIdentifier TargetId;
    public required DateTimeOffset SentAtUtc;
    public required byte[] Payload;
}
