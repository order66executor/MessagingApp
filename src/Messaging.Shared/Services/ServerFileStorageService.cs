namespace Messaging.Shared.Services;

// File storage service implemetation
public class FileStorageService : IFileStorageService {
    private readonly string directory;

    public FileStorageService(string directory) {
        this.directory = directory;
    }

    // Write file data to disk and return the Guid
    public async Task<string> SaveFileAsync(string fileName, byte[] data, CancellationToken ct = default) {

        string fileId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(directory); // Create directory if it does not exist yet

        string sanitizedFileName = Path.GetFileName(fileName);
        string savePath = Path.Combine(directory, $"{fileId}_{sanitizedFileName}");
        
        await File.WriteAllBytesAsync(savePath, data, ct); // Writ to disk
        return fileId;

    }

    // Load file into RAM, returns fileName and data tuple
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