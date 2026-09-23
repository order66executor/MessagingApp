using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
public class Segment {
    [Key(0)]
    public required string Id;
    [Key(1)]
    public required int Size { get; set; }
    [Key(2)]
    public required byte[] Data { get; set; }
    [Key(3)]
    public required bool IsEnd { get; set; }
}
