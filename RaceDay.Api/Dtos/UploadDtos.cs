using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>An image file sent as multipart/form-data (JPG, PNG or WebP, max 5 MB).</summary>
public class ImageUploadRequest
{
    [Required] public IFormFile? File { get; set; }
}
