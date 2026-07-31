using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
public class SegmentPayload {
    [Key(0)]
    public required int SequenceNumber;
    [Key(1)]
    public required int SegmentSize;
    [Key(2)]
    public required byte[] SegmentData;
}
    