using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enter event categories. Organisers view and manage the enrolments for their events.</summary>
[Tags("Event Enrolments")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public EnrolmentsController(ApplicationDbContext db) => _db = db;

    private IQueryable<Enrolment> WithDetails() =>
        _db.Enrolments
            .Include(e => e.Participant).ThenInclude(p => p!.Role)
            .Include(e => e.Category).ThenInclude(c => c!.Event);

    /// <summary>Enter an event category as the logged-in Participant.</summary>
    /// <remarks>
    /// Expects categoryId and optional notes. The system records the link between the participant, the event
    /// (through the category) and the selected category. The event must be Published, the category and event must
    /// not be full, and your age (from your profile date of birth) must fit the category's age limits.
    /// If you previously cancelled this enrolment it is re-activated.
    /// </remarks>
    /// <returns>
    /// 201 with the enrolment, 400 if the event is not open or the age does not fit, 403 for Organisers,
    /// 404 if the category does not exist, 409 if already enrolled or full.
    /// </returns>
    [HttpPost("enrolments")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEnrolment([FromBody] CreateEnrolmentRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event)
            .SingleOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category?.Event is null) return NotFoundError("Category not found.");

        var ev = category.Event;
        if (ev.Status != EventStatuses.Published)
            return BadRequestError("This event is not open for enrolment.", "categoryId");

        var me = await _db.Users.SingleAsync(u => u.UserId == CurrentUserId);

        // Age rules (only checked when the category has age limits)
        if (category.AgeMin is not null || category.AgeMax is not null)
        {
            if (me.DateOfBirth is null)
                return BadRequestError("This category has age limits. Please add your date of birth to your profile first.", "dateOfBirth");

            var age = AgeOn(me.DateOfBirth.Value, ev.EventDate);
            if (category.AgeMin is not null && age < category.AgeMin)
                return BadRequestError($"You must be at least {category.AgeMin} years old for this category.", "categoryId");
            if (category.AgeMax is not null && age > category.AgeMax)
                return BadRequestError($"You must be {category.AgeMax} years old or younger for this category.", "categoryId");
        }

        var existing = await _db.Enrolments
            .SingleOrDefaultAsync(e => e.ParticipantId == me.UserId && e.CategoryId == category.CategoryId);
        if (existing is not null && existing.Status != EnrolmentStatuses.Cancelled)
            return ConflictError("You are already enrolled in this category.");

        // Capacity: cancelled enrolments don't count
        if (category.MaxEntries is not null)
        {
            var inCategory = await _db.Enrolments.CountAsync(e =>
                e.CategoryId == category.CategoryId && e.Status != EnrolmentStatuses.Cancelled);
            if (inCategory >= category.MaxEntries)
                return ConflictError("This category is full.");
        }
        if (ev.MaxParticipants is not null)
        {
            var inEvent = await _db.Enrolments.CountAsync(e =>
                e.Category!.EventId == ev.EventId && e.Status != EnrolmentStatuses.Cancelled);
            if (inEvent >= ev.MaxParticipants)
                return ConflictError("This event is full.");
        }

        var paymentStatus = category.EntryFee == 0 ? PaymentStatuses.Paid : PaymentStatuses.Pending;
        Enrolment enrolment;
        if (existing is not null)
        {
            enrolment = existing;
            enrolment.Status = EnrolmentStatuses.Confirmed;
            enrolment.EnrolmentDate = DateTime.UtcNow;
            enrolment.Notes = request.Notes;
            enrolment.PaymentStatus = paymentStatus;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantId = me.UserId,
                CategoryId = category.CategoryId,
                Status = EnrolmentStatuses.Confirmed,
                PaymentStatus = paymentStatus,
                Notes = request.Notes
            };
            _db.Enrolments.Add(enrolment);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index on (ParticipantId, CategoryId) caught a double-click / race.
            return ConflictError("You are already enrolled in this category.");
        }

        var saved = await WithDetails().AsNoTracking().SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        return CreatedAtAction(nameof(GetEnrolment), new { id = saved.EnrolmentId }, saved.ToDto());
    }

    /// <summary>List all of the logged-in Participant's own enrolments, with event and category details.</summary>
    /// <returns>200 with an array of enrolments, 401 if not logged in, 403 for Organisers.</returns>
    [HttpGet("enrolments/me")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var me = CurrentUserId;
        var list = await WithDetails().AsNoTracking()
            .Where(e => e.ParticipantId == me)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(list.Select(e => e.ToDto()).ToList());
    }

    /// <summary>List every enrolment for an event you own, with participant info (Organisers only).</summary>
    /// <returns>200 with an array of enrolments, 403 if not your event, 404 if the event does not exist.</returns>
    [HttpGet("events/{eventId:int}/enrolments")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundError("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only view enrolments for your own events.");

        var list = await WithDetails().AsNoTracking()
            .Where(e => e.Category!.EventId == eventId)
            .OrderBy(e => e.EnrolmentId)
            .ToListAsync();
        return Ok(list.Select(e => e.ToDto(includeParticipant: true)).ToList());
    }

    /// <summary>Get one enrolment. Participants can view their own; Organisers can view any under their events.</summary>
    /// <returns>200 with the enrolment, 403 if it is not yours, 404 if it does not exist.</returns>
    [HttpGet("enrolments/{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolment(int id)
    {
        var enrolment = await WithDetails().AsNoTracking().SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundError("Enrolment not found.");
        if (!CanAccess(enrolment)) return ForbiddenError("You do not have access to this enrolment.");
        return Ok(enrolment.ToDto(includeParticipant: IsOrganiser));
    }

    /// <summary>Cancel an enrolment (status becomes "Cancelled").</summary>
    /// <remarks>A Participant can cancel their own; an Organiser can cancel any enrolment under their events.</remarks>
    /// <returns>200 with the updated enrolment, 400 if the race is already completed, 403 if not allowed, 404 if it does not exist.</returns>
    [HttpPut("enrolments/{id:int}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await WithDetails().SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundError("Enrolment not found.");
        if (!CanAccess(enrolment)) return ForbiddenError("You do not have access to this enrolment.");
        if (enrolment.Status == EnrolmentStatuses.Completed)
            return BadRequestError("A completed enrolment cannot be cancelled.");

        enrolment.Status = EnrolmentStatuses.Cancelled;
        await _db.SaveChangesAsync();
        return Ok(enrolment.ToDto(includeParticipant: IsOrganiser));
    }

    /// <summary>Assign or change the bib number of an enrolment (Organiser of the event only).</summary>
    /// <returns>200 with the updated enrolment, 403 if not your event, 404 if it does not exist, 409 if the bib is already used in this event.</returns>
    [HttpPut("enrolments/{id:int}/bib")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignBib(int id, [FromBody] AssignBibRequest request)
    {
        var enrolment = await WithDetails().SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundError("Enrolment not found.");
        if (enrolment.Category!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenError("You can only assign bib numbers for your own events.");

        var bib = request.BibNumber.Trim();
        var eventId = enrolment.Category.EventId;
        var taken = await _db.Enrolments.AnyAsync(e =>
            e.EnrolmentId != id && e.BibNumber == bib && e.Category!.EventId == eventId);
        if (taken) return ConflictError("That bib number is already used in this event.");

        enrolment.BibNumber = bib;
        await _db.SaveChangesAsync();
        return Ok(enrolment.ToDto(includeParticipant: true));
    }

    /// <summary>A participant can access their own enrolment; an organiser can access enrolments under their events.</summary>
    private bool CanAccess(Enrolment e) =>
        (IsParticipant && e.ParticipantId == CurrentUserId) ||
        (IsOrganiser && e.Category?.Event?.OrganiserId == CurrentUserId);

    private static int AgeOn(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (on < dateOfBirth.AddYears(age)) age--;
        return age;
    }
}
