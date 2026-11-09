using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Who has enrolled for each of an organiser's events.</summary>
[Authorize(Roles = "Organiser")]
[Route("organiser")]
public class OrganiserEnrolmentsController : AppController
{
    public OrganiserEnrolmentsController(IRaceDayApi api) : base(api) { }

    [HttpGet("enrolments")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) return await LoadFailedAsync(result);

        return View("~/Views/Shared/EventPicker.cshtml", new EventPickerViewModel
        {
            Title = "Enrolments",
            Subtitle = "Choose an event to see who has enrolled, their categories and statuses.",
            ButtonText = "View enrolments",
            Action = "Manage",
            Controller = "OrganiserEnrolments",
            Events = result.Data!.OrderBy(e => e.EventDate).ToList()
        });
    }

    [HttpGet("events/{eventId:int}/enrolments")]
    public async Task<IActionResult> Manage(int eventId)
    {
        var ev = await Api.GetEventAsync(eventId);
        if (!ev.Success) return await LoadFailedAsync(ev);
        if (ev.Data!.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Success) return await LoadFailedAsync(enrolments);

        return View(new EnrolmentsManageViewModel
        {
            Event = ev.Data,
            Enrolments = enrolments.Data!.OrderBy(e => e.CategoryName).ThenBy(e => e.EnrolmentDate).ToList()
        });
    }

    [HttpPost("events/{eventId:int}/enrolments/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int eventId, int id)
    {
        var result = await Api.CancelEnrolmentAsync(id);
        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess("The enrolment has been cancelled.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    [HttpPost("events/{eventId:int}/enrolments/{id:int}/bib")]
    public async Task<IActionResult> Bib(int eventId, int id, string? bibNumber)
    {
        if (string.IsNullOrWhiteSpace(bibNumber))
        {
            AlertError("Please enter a bib number.");
            return RedirectToAction(nameof(Manage), new { eventId });
        }

        var result = await Api.AssignBibAsync(id, bibNumber.Trim());
        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess($"Bib {result.Data!.BibNumber} assigned.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }
}
