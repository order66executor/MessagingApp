
namespace Messaging.Server.Data;

// This class is used as a base for a database table that tracks the highest acknowledged message ids per conversation per sender.
// This is how we can identify duplicate messages and issues with acking
public class DbAckCounter {
    public int Id;
    public required string ConversationKey { get; set;}
    public required string SenderUsername { get; set; }
    public required long HighestAck { get; set; }
}