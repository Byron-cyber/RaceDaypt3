using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>In-memory stand-in for Azure Blob Storage so the tests need no Azure account.</summary>
public class FakeBlobStorageService : IBlobStorageService
{
    public ConcurrentDictionary<string, (byte[] Data, string ContentType)> Blobs { get; } = new();

    /// <summary>Set to true to simulate Azure being unavailable.</summary>
    public bool FailUploads { get; set; }

    public async Task<string> UploadAsync(string folder, Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        if (FailUploads) throw new InvalidOperationException("Blob storage is down (simulated).");

        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct);
        var name = $"{folder}/{Guid.NewGuid():N}{extension}";
        Blobs[name] = (memory.ToArray(), contentType);
        return name;
    }

    public Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        BlobFile? file = Blobs.TryGetValue(blobName, out var blob)
            ? new BlobFile(new MemoryStream(blob.Data), blob.ContentType)
            : null;
        return Task.FromResult(file);
    }

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        Blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}
