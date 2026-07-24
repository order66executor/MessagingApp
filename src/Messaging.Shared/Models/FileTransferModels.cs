namespace Messaging.Shared.Models;

public class FileUploadPayload {
    public required string FileName { get; set; }
    public required byte[] FileData { get; set; }
}

public class FileNotificationPayload {
    public required string FileId { get; set; }
    public required string FileName { get; set; }
    public required long FileSize { get; set; }
}

public class FileRequestPayload {
    public required string FileId { get; set; }
}

public class FileResponsePayload {
    public required string FileId { get; set; }
    public required string FileName { get; set; }
    public required byte[] FileData { get; set; }
}
