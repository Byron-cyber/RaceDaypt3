using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Shows event banners and profile pictures. The images live in Azure Blob Storage behind the API;
/// this controller only asks the API for them, so the browser never talks to Azure.
/// </summary>
[AllowAnonymous]
public class MediaController : AppController
{
    public MediaController(IRaceDayApi api) : base(api) { }

    [HttpGet("media/{**path}")]
    public async Task<IActionResult> Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..")) return NotFound();

        var image = await Api.GetImageAsync(path);
        if (image is null) return NotFound();

        Response.Headers.CacheControl = "public,max-age=3600";
        return File(image.Bytes, image.ContentType);
    }
}
