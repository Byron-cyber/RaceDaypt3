using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>View and update your own profile. Organisers can look up other users.</summary>
[Tags("User Profile")]
public class UsersController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ApplicationDbContext db, IBlobStorageService blobs, ILogger<UsersController> logger)
    {
        _db = db;
        _blobs = blobs;
        _logger = logger;
    }

    /// <summary>Get the profile of the logged-in user.</summary>
    /// <returns>200 with the profile (no password hash), 401 if not logged in.</returns>
    [HttpGet("users/me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFoundError("User not found.");
        return Ok(user.ToDto());
    }

    /// <summary>Update your own first name, last name, phone number and date of birth.</summary>
    /// <remarks>The email address and role cannot be changed.</remarks>
    /// <returns>200 with the updated profile, 400 for invalid data, 401 if not logged in.</returns>
    [HttpPut("users/me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequestError("Date of birth cannot be in the future.", "dateOfBirth");

        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFoundError("User not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.DateOfBirth = request.DateOfBirth;
        await _db.SaveChangesAsync();

        return Ok(user.ToDto());
    }

    /// <summary>Upload or replace your own profile picture.</summary>
    /// <remarks>Send the image as multipart/form-data in a field called "file" (JPG, PNG or WebP, max 5 MB). It is stored in Azure Blob Storage.</remarks>
    /// <returns>200 with the updated profile, 400 for an invalid image, 401 if not logged in, 503 if image storage is unavailable.</returns>
    [HttpPost("users/me/profile-picture")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadProfilePicture([FromForm] ImageUploadRequest request, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId, ct);
        if (user is null) return NotFoundError("User not found.");

        var (image, error) = await ImageUploads.ValidateAsync(request.File);
        if (image is null) return BadRequestError(error!, "file");

        string blobName;
        try
        {
            await using var stream = request.File!.OpenReadStream();
            blobName = await _blobs.UploadAsync("profiles", stream, image.ContentType, image.Extension, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Profile picture upload failed for user {UserId}", user.UserId);
            return Error(StatusCodes.Status503ServiceUnavailable, "Image storage is currently unavailable. Please try again later.");
        }

        var oldBlob = ImageUploads.ToBlobName(user.ProfilePictureUrl);
        user.ProfilePictureUrl = ImageUploads.ToUrl(blobName);
        await _db.SaveChangesAsync(ct);

        if (oldBlob is not null)
        {
            try { await _blobs.DeleteAsync(oldBlob, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete old profile picture {Blob}", oldBlob); }
        }
        return Ok(user.ToDto());
    }

    /// <summary>Get limited public information about a user (Organisers only).</summary>
    /// <returns>200 with name, email and role, 403 for Participants, 404 if the user does not exist.</returns>
    [HttpGet("users/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == id);
        if (user is null) return NotFoundError("User not found.");
        return Ok(user.ToSummary());
    }
}
