using System.Collections.Concurrent;

using MessagePack.Formatters;

using Messaging.Shared.Models;
using System.Security.Cryptography;

namespace Messaging.Shared.Services;

// File storage service implemetation
public class FileStorageService : IFileStorageService {
    public string DefaultDirectory { get; set; }
    private readonly ConcurrentDictionary<Guid, FileStream> streams;

    public FileStorageService(string directory) {
        DefaultDirectory = directory;
        streams = [ ];
    }

    public string GetPathOfStream(Guid guid) {
        return streams.TryGetValue(guid, out var stream) ? stream.Name : string.Empty;
    }

    public string GetAbsolutePath(string relativePath) {
        return Path.Combine(DefaultDirectory, relativePath);
    }

    public void CreateWriteStream(Guid guid, string fileName) {
        Directory.CreateDirectory(DefaultDirectory); // Create DefaultDirectory if it does not exist yet
        string savePath = Path.Combine(DefaultDirectory, fileName);

        if (Path.Exists(savePath)) {
            int i = 1;

            string candidate;

            do {
                candidate = Path.Combine(DefaultDirectory, $"{i}_{fileName}");
                ++i;
            } while (Path.Exists(candidate));

            savePath = candidate;
        }

        streams.TryAdd(guid, new FileStream(savePath, FileMode.OpenOrCreate));
    }

    public void CloseStream(Guid guid) {
        streams.TryRemove(guid, out var stream);
        if (stream is null) return;

        stream.Close();
    }

    public long GetFileSize(string path) {
        return new FileInfo(path).Length;
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(string path) {

        if (!Path.Exists(path)) yield break;

        FileStream stream = new(path, FileMode.Open);
        byte[] buffer = new byte[8192*8];
        int read;

        while ((read = await stream.ReadAsync(buffer, CancellationToken.None)) > 0) {
            yield return buffer.AsMemory(0, read);
        }

    }

    // Checks SHA256 hash of specified file against provided hash
    public async Task<bool> CheckSha256Async(string path, string hash) {
        return (await GetSha256Async(path)) == hash;
    }

    // Computes SHA256 hash of specified file
    public async Task<string> GetSha256Async(string path) {
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(File.OpenRead(path)));
    }

    public async Task<bool> WriteAsync(Guid guid, byte[] data) {
        if (!streams.TryGetValue(guid, out FileStream? stream)) {
            return false;
        }

        await stream.WriteAsync(data);
        return true;

    }

    // Write file data to disk and return the Guid
    public async Task SaveFileAsync(string fileName, byte[] data, CancellationToken ct = default) {

        Directory.CreateDirectory(DefaultDirectory); // Create DefaultDirectory if it does not exist yet
        string savePath = Path.Combine(DefaultDirectory, fileName);

        if (Path.Exists(savePath)) {
            int i = 1;
            while (true) {
                if (!Path.Exists(savePath + $"_{i}")) {
                    savePath += $"_{i}";
                    break;
                }
                ++i;
            }
        }
        
        await File.WriteAllBytesAsync(savePath, data, ct); // Write to disk
    }

    // Load file into RAM, returns fileName and data tuple
    public async Task<(string FileName, byte[] Data)?> GetFileAsync(string fileId, CancellationToken ct = default) {

        var filePath = Directory.GetFiles(DefaultDirectory, fileId).FirstOrDefault();
        if (filePath != null) {
            byte[] fileData = await File.ReadAllBytesAsync(filePath, ct);
            string fileName = Path.GetFileName(filePath)[(fileId.Length + 1)..];
            return (fileName, fileData);
        }
        else return null;
    }

}