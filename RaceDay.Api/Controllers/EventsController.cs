using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Browse events (everyone) and manage events (Organisers, own events only).</summary>
[Tags("Events")]
public class EventsController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<EventsController> _logger;

    public EventsController(ApplicationDbContext db, IBlobStorageService blobs, ILogger<EventsController> logger)
    {
        _db = db;
        _blobs = blobs;
        _logger = logger;
    }

    /// <summary>List events that are open to the public (Draft events are never listed).</summary>
    /// <remarks>
    /// Optional filters: city, province, eventType (Run, Walk, Cycle), dateFrom, dateTo (YYYY-MM-DD), status (Published, Cancelled, Completed).
    /// Pagination: page (default 1) and pageSize (default 20, max 100). The total number of matches is returned
    /// in the X-Total-Count response header.
    /// </remarks>
    /// <returns>200 with an array of event summaries ordered by date.</returns>
    [HttpGet("events")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? city, [FromQuery] string? province, [FromQuery] string? eventType,
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) return BadRequestError("page must be 1 or more.", "page");
        if (pageSize < 1 || pageSize > 100) return BadRequestError("pageSize must be between 1 and 100.", "pageSize");

        var query = _db.Events.AsNoTracking().Where(e => e.Status != EventStatuses.Draft);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var match = EventStatuses.All.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null || match == EventStatuses.Draft)
                return BadRequestError("status must be Published, Cancelled or Completed.", "status");
            query = query.Where(e => e.Status == match);
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim().ToLower();
            query = query.Where(e => e.City.ToLower() == c);
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = EventTypes.All.FirstOrDefault(t => string.Equals(t, eventType.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type is null) return BadRequestError("eventType must be Run, Walk or Cycle.", "eventType");
            query = query.Where(e => e.EventType == type);
        }
        if (!string.IsNullOrWhiteSpace(province))
        {
            var p = province.Trim().ToLower();
            query = query.Where(e => e.Province.ToLower() == p);
        }
        if (dateFrom is not null) query = query.Where(e => e.EventDate >= dateFrom);
        if (dateTo is not null) query = query.Where(e => e.EventDate <= dateTo);

        var total = await query.CountAsync();
        var events = await query
            .OrderBy(e => e.EventDate).ThenBy(e => e.EventId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(events.Select(e => e.ToDto()).ToList());
    }

    /// <summary>Get the full details of one event, including its categories.</summary>
    /// <remarks>Draft events are only visible to the Organiser who owns them.</remarks>
    /// <returns>200 with the event and its categories, 404 if it does not exist (or is someone else's draft).</returns>
    [HttpGet("events/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await _db.Events.AsNoTracking().Include(e => e.Categories)
            .SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null || !CanSee(ev)) return NotFoundError("Event not found.");
        return Ok(ev.ToDetailDto());
    }

    /// <summary>List every event created by the logged-in Organiser (including drafts).</summary>
    /// <returns>200 with an array of events, 401 if not logged in, 403 for Participants.</returns>
    [HttpGet("events/my")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEvents()
    {
        var me = CurrentUserId;
        var events = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == me)
            .OrderBy(e => e.EventDate).ThenBy(e => e.EventId)
            .ToListAsync();

        // Active enrolments per event, for the organiser dashboard
        var ids = events.Select(e => e.EventId).ToList();
        var counts = await _db.Enrolments
            .Where(en => ids.Contains(en.Category!.EventId) && en.Status != EnrolmentStatuses.Cancelled)
            .GroupBy(en => en.Category!.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        return Ok(events.Select(e =>
        {
            var dto = e.ToDto();
            dto.EnrolmentCount = counts.GetValueOrDefault(e.EventId);
            return dto;
        }).ToList());
    }

    /// <summary>Create a new event (Organisers only). The logged-in Organiser becomes its owner.</summary>
    /// <remarks>
    /// Expects title, eventDate (YYYY-MM-DD), location, city and province. Optional: description,
    /// startTime (HH:mm), eventType ("Run" (default), "Walk" or "Cycle"), distanceKm, status ("Draft" or "Published",
    /// default Draft) and maxParticipants. The banner image is uploaded separately with POST /api/events/{id}/banner.
    /// </remarks>
    /// <returns>201 with the created event, 400 for invalid data, 401 if not logged in, 403 for Participants.</returns>
    [HttpPost("events")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        var status = EventStatuses.All.FirstOrDefault(s =>
            string.Equals(s, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is not (EventStatuses.Draft or EventStatuses.Published))
            return BadRequestError("status must be 'Draft' or 'Published' when creating an event.", "status");

        var eventType = EventTypes.All.FirstOrDefault(t =>
            string.Equals(t, request.EventType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (eventType is null) return BadRequestError("eventType must be Run, Walk or Cycle.", "eventType");

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Title = request.Title.Trim(),
            Description = request.Description,
            EventDate = request.EventDate!.Value,
            StartTime = request.StartTime,
            Location = request.Location.Trim(),
            City = request.City.Trim(),
            Province = request.Province.Trim(),
            Status = status,
            EventType = eventType,
            DistanceKm = request.DistanceKm,
            MaxParticipants = request.MaxParticipants
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Update an event you own. Only the fields you send are changed.</summary>
    /// <returns>200 with the updated event, 400 for invalid data, 403 if it is not your event, 404 if it does not exist.</returns>
    [HttpPut("events/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpdateEventRequest request)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundError("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only change your own events.");

        if (request.Status is not null)
        {
            var status = EventStatuses.All.FirstOrDefault(s =>
                string.Equals(s, request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (status is null)
                return BadRequestError("status must be Draft, Published, Cancelled or Completed.", "status");
            ev.Status = status;
        }
        if (request.EventType is not null)
        {
            var type = EventTypes.All.FirstOrDefault(t =>
                string.Equals(t, request.EventType.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type is null) return BadRequestError("eventType must be Run, Walk or Cycle.", "eventType");
            ev.EventType = type;
        }
        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title)) return BadRequestError("title cannot be empty.", "title");
            ev.Title = request.Title.Trim();
        }
        if (request.Location is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Location)) return BadRequestError("location cannot be empty.", "location");
            ev.Location = request.Location.Trim();
        }
        if (request.City is not null)
        {
            if (string.IsNullOrWhiteSpace(request.City)) return BadRequestError("city cannot be empty.", "city");
            ev.City = request.City.Trim();
        }
        if (request.Province is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Province)) return BadRequestError("province cannot be empty.", "province");
            ev.Province = request.Province.Trim();
        }
        if (request.Description is not null) ev.Description = request.Description;
        if (request.EventDate is not null) ev.EventDate = request.EventDate.Value;
        if (request.StartTime is not null) ev.StartTime = request.StartTime;
        if (request.MaxParticipants is not null) ev.MaxParticipants = request.MaxParticipants;
        if (request.DistanceKm is not null) ev.DistanceKm = request.DistanceKm;

        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Upload or replace the banner image of an event you own (Organisers only).</summary>
    /// <remarks>
    /// Send the image as multipart/form-data in a field called "file" (JPG, PNG or WebP, max 5 MB).
    /// The image is stored in Azure Blob Storage and the event's imageUrl is set to the path that serves it.
    /// </remarks>
    /// <returns>200 with the updated event, 400 for an invalid image, 403 if it is not your event, 404 if it does not exist, 503 if image storage is unavailable.</returns>
    [HttpPost("events/{id:int}/banner")]
    [Authorize(Roles = RoleNames.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int id, [FromForm] ImageUploadRequest request, CancellationToken ct)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id, ct);
        if (ev is null) return NotFoundError("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only change the banner of your own events.");

        var (image, error) = await ImageUploads.ValidateAsync(request.File);
        if (image is null) return BadRequestError(error!, "file");

        string blobName;
        try
        {
            await using var stream = request.File!.OpenReadStream();
            blobName = await _blobs.UploadAsync("events", stream, image.ContentType, image.Extension, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Banner upload failed for event {EventId}", id);
            return Error(StatusCodes.Status503ServiceUnavailable, "Image storage is currently unavailable. Please try again later.");
        }

        var oldBlob = ImageUploads.ToBlobName(ev.ImageUrl);
        ev.ImageUrl = ImageUploads.ToUrl(blobName);
        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (oldBlob is not null)
        {
            try { await _blobs.DeleteAsync(oldBlob, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete old banner {Blob}", oldBlob); }
        }
        return Ok(ev.ToDto());
    }

    /// <summary>Cancel an event you own (its status becomes "Cancelled"; the data is kept).</summary>
    /// <returns>204 when cancelled, 403 if it is not your event, 404 if it does not exist.</returns>
    [HttpDelete("events/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundError("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only cancel your own events.");

        ev.Status = EventStatuses.Cancelled;
        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Draft events are only visible to their owner.</summary>
    private bool CanSee(Event ev) =>
        ev.Status != EventStatuses.Draft || (IsAuthenticated && ev.OrganiserId == CurrentUserId);
}
