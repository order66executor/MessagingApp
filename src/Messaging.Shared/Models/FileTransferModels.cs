using MessagePack;

namespace Messaging.Shared.Models;



// File-related payload structures
[MessagePackObject]
public class FileUploadPayload {
    [Key(0)]
    public required string FileName { get; set; }
    [Key(1)]
    public required long FileSize { get; set; }
    [Key(2)]
    public required string Sha256Hash { get; set; }
    [Key(3)]
    public required string ClientTransferId { get; set; }
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
    public required string Sha256Hash { get; set; }
}

[MessagePackObject]
public class FileTransferReadyPayload {
    [Key(0)]
    public required string FileId { get; set; }
    [Key(1)]
    public required string ClientTransferId { get; set; }
}
