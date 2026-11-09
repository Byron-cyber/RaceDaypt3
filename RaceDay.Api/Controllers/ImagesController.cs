using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Common;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Serves images stored in Azure Blob Storage. Clients never talk to Azure directly.</summary>
[Tags("Images")]
public class ImagesController : ApiControllerBase
{
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(IBlobStorageService blobs, ILogger<ImagesController> logger)
    {
        _blobs = blobs;
        _logger = logger;
    }

    /// <summary>Get an uploaded image (event banner or profile picture) by its path.</summary>
    /// <remarks>Use the path returned in an event's imageUrl or a user's profilePictureUrl, e.g. /api/images/events/abc.jpg.</remarks>
    /// <returns>200 with the image, 404 if it does not exist, 503 if image storage is unavailable.</returns>
    [HttpGet("images/{**blobName}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetImage(string blobName, CancellationToken ct)
    {
        // Only the two folders this API writes to can be read, and never anything that climbs out of them
        var allowed = (blobName.StartsWith("events/") || blobName.StartsWith("profiles/")) && !blobName.Contains("..");
        if (!allowed) return NotFoundError("Image not found.");

        try
        {
            var file = await _blobs.DownloadAsync(blobName, ct);
            if (file is null) return NotFoundError("Image not found.");
            return File(file.Content, file.ContentType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not read image {BlobName} from blob storage", blobName);
            return Error(StatusCodes.Status503ServiceUnavailable, "Image storage is currently unavailable.");
        }
    }
}
