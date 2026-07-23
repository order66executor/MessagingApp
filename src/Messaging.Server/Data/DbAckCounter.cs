
namespace Messaging.Server.Data;

public class DbAckCounter {
    public int Id;
    public required string ConversationKey { get; set;}
    public required string SenderUsername { get; set; }
    public required long HighestAck { get; set; }
}