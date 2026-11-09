using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Capture finish times and positions, then publish the results.</summary>
[Authorize(Roles = "Organiser")]
[Route("organiser")]
public class OrganiserResultsController : AppController
{
    public OrganiserResultsController(IRaceDayApi api) : base(api) { }

    [HttpGet("results")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) return await LoadFailedAsync(result);

        return View("~/Views/Shared/EventPicker.cshtml", new EventPickerViewModel
        {
            Title = "Results",
            Subtitle = "Choose an event to capture finish times and positions, then publish the results.",
            ButtonText = "Enter results",
            Action = "Manage",
            Controller = "OrganiserResults",
            Events = result.Data!.OrderByDescending(e => e.EventDate).ToList()
        });
    }

    [HttpGet("events/{eventId:int}/results")]
    public async Task<IActionResult> Manage(int eventId)
    {
        var ev = await Api.GetEventAsync(eventId);
        if (!ev.Success) return await LoadFailedAsync(ev);
        if (ev.Data!.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Success) return await LoadFailedAsync(enrolments);

        var results = await Api.GetEventResultsAsync(eventId);
        if (!results.Success) return await LoadFailedAsync(results);

        var byEnrolment = results.Data!.ToDictionary(r => r.EnrolmentId);
        var rows = enrolments.Data!
            .Where(e => e.Status != "Cancelled")
            .OrderBy(e => e.CategoryName).ThenBy(e => e.Participant?.LastName)
            .Select(e => new ResultRow { Enrolment = e, Result = byEnrolment.GetValueOrDefault(e.EnrolmentId) })
            .ToList();

        return View(new ResultsManageViewModel { Event = ev.Data, Rows = rows });
    }

    /// <summary>Saves one participant's result (creates it, or updates it if one already exists).</summary>
    [HttpPost("events/{eventId:int}/results/save")]
    public async Task<IActionResult> Save(int eventId, ResultInputModel input)
    {
        var status = Array.Find(Helpers.Lookups.ResultStatuses, s => s == input.Status) ?? "Finished";

        TimeSpan? finishTime = null;
        if (!string.IsNullOrWhiteSpace(input.FinishTime))
        {
            // Only hh:mm:ss is accepted, so "42:35" can't be mistaken for 42 hours
            if (!TimeSpan.TryParseExact(input.FinishTime.Trim(), @"h\:mm\:ss", CultureInfo.InvariantCulture, out var parsed))
            {
                AlertError("Enter the finish time as hours:minutes:seconds, for example 01:42:35.");
                return RedirectToAction(nameof(Manage), new { eventId });
            }
            finishTime = parsed;
        }

        if (status == "Finished" && finishTime is null)
        {
            AlertError("A finisher needs a finish time. Choose DNF, DNS or DQ if they did not finish.");
            return RedirectToAction(nameof(Manage), new { eventId });
        }

        var request = new SaveResultRequest
        {
            EnrolmentId = input.EnrolmentId,
            FinishTime = finishTime,
            OverallPosition = input.OverallPosition,
            CategoryPosition = input.CategoryPosition,
            Status = status
        };

        var result = input.ResultId is { } resultId
            ? await Api.UpdateResultAsync(resultId, request)
            : await Api.CreateResultAsync(request);

        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess($"Result saved for {result.Data!.ParticipantName}.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    [HttpPost("events/{eventId:int}/results/{id:int}/delete")]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await Api.DeleteResultAsync(id);
        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess("Result removed.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    /// <summary>Publishing marks the event as Completed, which makes the results visible to participants.</summary>
    [HttpPost("events/{eventId:int}/results/publish")]
    public async Task<IActionResult> Publish(int eventId)
    {
        var result = await Api.UpdateEventAsync(eventId, new SaveEventRequest { Status = "Completed" });
        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess("Results published. Participants can now see them under My Results.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    [HttpPost("events/{eventId:int}/results/unpublish")]
    public async Task<IActionResult> Unpublish(int eventId)
    {
        var result = await Api.UpdateEventAsync(eventId, new SaveEventRequest { Status = "Published" });
        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess("Results are hidden from participants again.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }
}
