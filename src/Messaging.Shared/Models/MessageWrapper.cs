namespace Messaging.Shared.Models;

public record MessageWrapper {
    public long Id { get; set; }                      // DB auto-increment primary key
    public required string ConversationKey { get; set; }  // "Alice::Bob" (sorted)
    public required long SequenceId { get; set; }         // per-conversation per-user(!) counter, never resets
    public required string SenderUsername { get; set; }
    public required string ReceiverUsername { get; set; }
    public required byte[] SerializedMessageData { get; set; } // MessageData as JSON bytes
    public required DateTime StoredAtUtc { get; set; }
    public required MessageState State { get; set; }
}
