using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

/// <summary>Settings in appsettings.json section "AzureBlob".</summary>
public class BlobStorageOptions
{
    /// <summary>Azure Storage connection string. "UseDevelopmentStorage=true" targets the local Azurite emulator.</summary>
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    public string ContainerName { get; set; } = "raceday-images";
}

public record BlobFile(Stream Content, string ContentType);

/// <summary>All file handling goes through this interface (the MVC app never talks to Azure directly).</summary>
public interface IBlobStorageService
{
    /// <summary>Uploads a file and returns the blob name, e.g. "events/3f2a....jpg".</summary>
    Task<string> UploadAsync(string folder, Stream content, string contentType, string extension, CancellationToken ct = default);

    /// <summary>Downloads a blob, or returns null if it does not exist.</summary>
    Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default);

    Task DeleteAsync(string blobName, CancellationToken ct = default);
}

/// <summary>Azure Blob Storage implementation. The container is private; images are served through the API.</summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobStorageOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private BlobContainerClient? _container;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options) => _options = options.Value;

   
    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        if (_container is not null) return _container;

        await _lock.WaitAsync(ct);
        try
        {
            if (_container is null)
            {
                var client = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
                await client.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _container = client;
            }
        }
        finally
        {
            _lock.Release();
        }
        return _container;
    }

    public async Task<string> UploadAsync(string folder, Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        return blobName;
    }

    public async Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        try
        {
            var response = await container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            return new BlobFile(response.Value.Content, response.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }
}
