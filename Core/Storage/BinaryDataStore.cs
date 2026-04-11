using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Storage;

public interface IBinaryDataStore {
    Task<string> SaveAsync(Stream stream, string fileName, string mimeType, CancellationToken ct);
    Task<Stream> LoadAsync(string storageId, CancellationToken ct);
    Task DeleteAsync(string storageId, CancellationToken ct);
}

public sealed class LocalBinaryDataStore : IBinaryDataStore {
    private readonly string _basePath;
    private readonly ILogger<LocalBinaryDataStore> _log;

    public LocalBinaryDataStore(string basePath, ILogger<LocalBinaryDataStore> log) {
        _basePath = basePath;
        _log = log;
        if (!Directory.Exists(_basePath)) {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> SaveAsync(Stream stream, string fileName, string mimeType, CancellationToken ct) {
        var storageId = $"{Guid.NewGuid():N}_{fileName}";
        var filePath = Path.Combine(_basePath, storageId);

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.CopyToAsync(fileStream, ct);
        
        _log.LogDebug("Saved binary data to {Path}", filePath);
        return storageId;
    }

    public Task<Stream> LoadAsync(string storageId, CancellationToken ct) {
        var filePath = Path.Combine(_basePath, storageId);
        if (!File.Exists(filePath)) {
            throw new FileNotFoundException("Binary data not found.", filePath);
        }

        return Task.FromResult<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true));
    }

    public Task DeleteAsync(string storageId, CancellationToken ct) {
        var filePath = Path.Combine(_basePath, storageId);
        if (File.Exists(filePath)) {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
}
