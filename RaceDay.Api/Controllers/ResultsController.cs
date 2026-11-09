using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Organisers capture finish times and positions. Participants view their own results.</summary>
[Tags("Results")]
public class ResultsController : ApiControllerBase
{
    private static readonly TimeSpan MaxTime = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _db;

    public ResultsController(ApplicationDbContext db) => _db = db;

    private IQueryable<Result> WithDetails() =>
        _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event);

    /// <summary>Capture the result of an enrolment (Organiser of the event only).</summary>
    /// <remarks>
    /// Expects enrolmentId and status ("Finished", "DNF", "DNS" or "DQ"). A "Finished" result needs finishTime (HH:mm:ss).
    /// Optional: overallPosition, categoryPosition, chipTime, gunTime. There can only be one result per enrolment.
    /// A finished result marks the enrolment as Completed.
    /// </remarks>
    /// <returns>201 with the result, 400 for invalid data, 403 if not your event, 404 if the enrolment does not exist, 409 if a result already exists.</returns>
    [HttpPost("results")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateResult([FromBody] CreateResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .SingleOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);
        if (enrolment is null) return NotFoundError("Enrolment not found.");
        if (enrolment.Category!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenError("You can only capture results for your own events.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled)
            return BadRequestError("Cannot capture a result for a cancelled enrolment.", "enrolmentId");
        if (await _db.Results.AnyAsync(r => r.EnrolmentId == request.EnrolmentId))
            return ConflictError("A result already exists for this enrolment.");

        var status = ResultStatuses.All.FirstOrDefault(s =>
            string.Equals(s, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null) return BadRequestError("status must be Finished, DNF, DNS or DQ.", "status");

        var problem = Validate(status, request.FinishTime, request.ChipTime, request.GunTime);
        if (problem is not null) return problem;

        var result = new Result
        {
            EnrolmentId = enrolment.EnrolmentId,
            FinishTime = request.FinishTime,
            OverallPosition = request.OverallPosition,
            CategoryPosition = request.CategoryPosition,
            Status = status,
            ChipTime = request.ChipTime,
            GunTime = request.GunTime,
            CapturedByUserId = CurrentUserId
        };
        _db.Results.Add(result);
        if (status == ResultStatuses.Finished) enrolment.Status = EnrolmentStatuses.Completed;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictError("A result already exists for this enrolment.");
        }

        var saved = await WithDetails().AsNoTracking().SingleAsync(r => r.ResultId == result.ResultId);
        return CreatedAtAction(nameof(GetResult), new { id = saved.ResultId }, await ToDtoAsync(saved));
    }

    /// <summary>List the logged-in Participant's race history: their results for events whose results have been published.</summary>
    /// <remarks>Each result includes the event, category, finish time, overall position and totalFinishers (for "47th of 312").</remarks>
    /// <returns>200 with an array of results, 401 if not logged in, 403 for Organisers.</returns>
    [HttpGet("results/me")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var me = CurrentUserId;
        var list = await WithDetails().AsNoTracking()
            .Where(r => r.Enrolment!.ParticipantId == me
                        && r.Enrolment.Category!.Event!.Status == EventStatuses.Completed)
            .ToListAsync();
        var dtos = await ToDtosAsync(list);
        return Ok(dtos.OrderByDescending(d => d.EventDate).ToList());
    }

    /// <summary>The results list for an event, best position first.</summary>
    /// <remarks>
    /// Everyone can see the results once the Organiser has published them (the event status is "Completed").
    /// Before that, only the Organiser who owns the event can see them.
    /// </remarks>
    /// <returns>200 with an ordered array of results, 404 if the event does not exist or its results are not published yet.</returns>
    [HttpGet("events/{eventId:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundError("Event not found.");

        var isOwner = IsAuthenticated && IsOrganiser && ev.OrganiserId == CurrentUserId;
        if (ev.Status != EventStatuses.Completed && !isOwner)
            return NotFoundError("Results for this event have not been published yet.");

        var list = await WithDetails().AsNoTracking()
            .Where(r => r.Enrolment!.Category!.EventId == eventId)
            .ToListAsync();
        var dtos = await ToDtosAsync(list);
        return Ok(dtos
            .OrderBy(d => d.OverallPosition ?? int.MaxValue)
            .ThenBy(d => d.FinishTime ?? TimeSpan.MaxValue)
            .ToList());
    }

    /// <summary>Get one result. Participants can view their own; Organisers can view results under their events.</summary>
    /// <returns>200 with the result, 403 if it is not yours, 404 if it does not exist.</returns>
    [HttpGet("results/{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult(int id)
    {
        var result = await WithDetails().AsNoTracking().SingleOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundError("Result not found.");

        var ev = result.Enrolment!.Category!.Event!;
        var allowed = (IsParticipant && result.Enrolment.ParticipantId == CurrentUserId && ev.Status == EventStatuses.Completed) ||
                      (IsOrganiser && ev.OrganiserId == CurrentUserId);
        if (!allowed) return ForbiddenError("You do not have access to this result.");
        return Ok(await ToDtoAsync(result));
    }

    /// <summary>Correct a result you captured (e.g. fix a time or status). Only the fields you send are changed.</summary>
    /// <returns>200 with the updated result, 400 for invalid data, 403 if not your event, 404 if it does not exist.</returns>
    [HttpPut("results/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] UpdateResultRequest request)
    {
        var result = await WithDetails().SingleOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundError("Result not found.");
        if (result.Enrolment!.Category!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenError("You can only change results for your own events.");

        var status = result.Status;
        if (request.Status is not null)
        {
            var match = ResultStatuses.All.FirstOrDefault(s =>
                string.Equals(s, request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) return BadRequestError("status must be Finished, DNF, DNS or DQ.", "status");
            status = match;
        }

        var finish = request.FinishTime ?? result.FinishTime;
        var chip = request.ChipTime ?? result.ChipTime;
        var gun = request.GunTime ?? result.GunTime;
        var problem = Validate(status, finish, chip, gun);
        if (problem is not null) return problem;

        result.Status = status;
        result.FinishTime = finish;
        result.ChipTime = chip;
        result.GunTime = gun;
        if (request.OverallPosition is not null) result.OverallPosition = request.OverallPosition;
        if (request.CategoryPosition is not null) result.CategoryPosition = request.CategoryPosition;

        if (status == ResultStatuses.Finished && result.Enrolment.Status == EnrolmentStatuses.Confirmed)
            result.Enrolment.Status = EnrolmentStatuses.Completed;

        await _db.SaveChangesAsync();
        return Ok(await ToDtoAsync(result));
    }

    /// <summary>Remove a result you captured.</summary>
    /// <returns>204 when removed, 403 if not your event, 404 if it does not exist.</returns>
    [HttpDelete("results/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResult(int id)
    {
        var result = await WithDetails().SingleOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundError("Result not found.");
        if (result.Enrolment!.Category!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenError("You can only delete results for your own events.");

        if (result.Enrolment.Status == EnrolmentStatuses.Completed)
            result.Enrolment.Status = EnrolmentStatuses.Confirmed;

        _db.Results.Remove(result);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Converts results to DTOs and fills in how many people finished each event.</summary>
    private async Task<List<ResultDto>> ToDtosAsync(List<Result> results)
    {
        var eventIds = results.Select(r => r.Enrolment!.Category!.EventId).Distinct().ToList();
        var counts = await _db.Results
            .Where(r => r.Status == ResultStatuses.Finished && eventIds.Contains(r.Enrolment!.Category!.EventId))
            .GroupBy(r => r.Enrolment!.Category!.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        return results.Select(r =>
        {
            var dto = r.ToDto();
            dto.TotalFinishers = counts.GetValueOrDefault(dto.EventId);
            return dto;
        }).ToList();
    }

    private async Task<ResultDto> ToDtoAsync(Result result) => (await ToDtosAsync(new List<Result> { result })).Single();

    private ObjectResult? Validate(string status, TimeSpan? finish, TimeSpan? chip, TimeSpan? gun)
    {
        if (status == ResultStatuses.Finished && finish is null)
            return BadRequestError("finishTime is required when status is 'Finished'.", "finishTime");
        if (IsBadTime(finish)) return BadRequestError("finishTime must be between 00:00:00 and 23:59:59.", "finishTime");
        if (IsBadTime(chip)) return BadRequestError("chipTime must be between 00:00:00 and 23:59:59.", "chipTime");
        if (IsBadTime(gun)) return BadRequestError("gunTime must be between 00:00:00 and 23:59:59.", "gunTime");
        return null;
    }

    private static bool IsBadTime(TimeSpan? t) => t is { } v && (v < TimeSpan.Zero || v >= MaxTime);
}
