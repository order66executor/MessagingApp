namespace Messaging.Server.Services;

public interface IFileStorageService {
    public Task<string> SaveFileAsync(string fileName, byte[] data, CancellationToken ct = default);
    public Task<(string FileName, byte[] Data)?> GetFileAsync(string fileId, CancellationToken ct = default);

}