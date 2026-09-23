namespace Messaging.Shared.Services;

// Interface for FileStorageService implemetations
public interface IFileStorageService {
    public string DefaultDirectory { get; set; }
    Task SaveFileAsync(string fileName, byte[] data, CancellationToken ct = default);
    Task<(string FileName, byte[] Data)?> GetFileAsync(string fileId, CancellationToken ct = default);
    void CreateWriteStream(Guid guid, string fileName);
    Task<bool> WriteAsync(Guid guid, byte[] data);
    void CloseStream(Guid guid);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(string path);
    Task<string> GetSha256Async(string path);
    Task<bool> CheckSha256Async(string path, string hash);
    long GetFileSize(string path);

    // Returns the absolute path from a default-relative path
    string GetAbsolutePath(string relativePath);

    string GetPathOfStream(Guid guid);

}