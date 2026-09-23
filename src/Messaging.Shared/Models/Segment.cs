using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
public class Segment {
    [Key(0)]
    public required bool HasNextSegment { get; set; }
    [Key(1)]
    public required int Size { get; set; }
    [Key(2)]
    public required byte[] Data { get; set; }
}
