using MessagePack;

namespace Messaging.Shared.Models;

[MessagePackObject]
public class FileUploadPayload {
    [Key(0)]
    public required string FileName { get; set; }
    [Key(1)]
    public required byte[] FileData { get; set; }
}

[MessagePackObject]
public class FileNotificationPayload {
    [Key(0)]
    public required string FileId { get; set; }
    [Key(1)]
    public required string FileName { get; set; }
    [Key(2)]
    public required long FileSize { get; set; }
}

[MessagePackObject]
public class FileRequestPayload {
    [Key(0)]
    public required string FileId { get; set; }
}

[MessagePackObject]
public class FileResponsePayload {
    [Key(0)]
    public required string FileId { get; set; }
    [Key(1)]
    public required string FileName { get; set; }
    [Key(2)]
    public required byte[] FileData { get; set; }
}
