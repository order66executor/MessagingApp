using Messaging.Shared.Models;

namespace Messaging.Shared.Services;

public class SegmentGenerator {
    private int sequenceNumber;
    private readonly int segmentSize;
    private readonly Stream stream;
    public bool CanRead { get; private set; } = true;

    public SegmentGenerator(Stream stream, int segmentSize) {
        sequenceNumber = 0;
        this.stream = stream;
        this.segmentSize = segmentSize;
    }

    public async Task<SegmentPayload> NextSegment(CancellationToken ct) {
        byte[] data = new byte[segmentSize];
        int bytesRead;

        bytesRead = await stream.ReadAsync(data, ct);
        if (bytesRead < segmentSize)
            CanRead = false;

        return new() {
            SequenceNumber = sequenceNumber++,
            SegmentSize = bytesRead,
            SegmentData = data
        };
    }


}