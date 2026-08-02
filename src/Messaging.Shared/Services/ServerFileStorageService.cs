namespace Messaging.Server.Services;

public class ServerFileStorageService : IFileStorageService {
    private readonly string directory;

    public ServerFileStorageService(string directory) {
        this.directory = directory;
    }

    public async Task<string> SaveFileAsync(string fileName, byte[] data, CancellationToken ct = default) {

        string fileId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(directory);

        string sanitizedFileName = Path.GetFileName(fileName);
        string savePath = Path.Combine(directory, $"{fileId}_{sanitizedFileName}");
        
        await File.WriteAllBytesAsync(savePath, data, ct);
        return fileId;

    }

    public async Task<(string FileName, byte[] Data)?> GetFileAsync(string fileId, CancellationToken ct = default) {

        var filePath = Directory.GetFiles(directory, $"{fileId}_*").FirstOrDefault();
        if (filePath != null) {
            byte[] fileData = await File.ReadAllBytesAsync(filePath, ct);
            string fileName = Path.GetFileName(filePath)[(fileId.Length + 1)..];
            return (fileName, fileData);
        }
        else return null;
    }

}