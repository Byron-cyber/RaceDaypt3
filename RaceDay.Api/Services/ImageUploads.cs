namespace RaceDay.Api.Services;

/// <summary>Validation and path helpers shared by the banner and profile-picture endpoints.</summary>
public static class ImageUploads
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string UrlPrefix = "/api/images/";

    public record Validated(string ContentType, string Extension);

    /// <summary>Checks size, content type and the real file signature (so a renamed .exe is rejected).</summary>
    public static async Task<(Validated? Ok, string? Error)> ValidateAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return (null, "Please choose an image file to upload.");
        if (file.Length > MaxBytes) return (null, "The image must be 5 MB or smaller.");

        var header = new byte[12];
        int read;
        await using (var stream = file.OpenReadStream())
        {
            read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        }
        if (read < 12) return (null, "That file is not a valid image.");

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return (new Validated("image/jpeg", ".jpg"), null);
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return (new Validated("image/png", ".png"), null);
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return (new Validated("image/webp", ".webp"), null);

        return (null, "Only JPG, PNG or WebP images are allowed.");
    }

    public static string ToUrl(string blobName) => UrlPrefix + blobName;

    /// <summary>Turns "/api/images/events/abc.jpg" back into the blob name "events/abc.jpg".</summary>
    public static string? ToBlobName(string? url) =>
        url is not null && url.StartsWith(UrlPrefix, StringComparison.Ordinal) ? url[UrlPrefix.Length..] : null;
}
