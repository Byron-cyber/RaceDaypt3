using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Participant home: browse upcoming events, see the details and enrol.</summary>
[Authorize(Roles = "Participant")]
[Route("events")]
public class EventsController : AppController
{
    private const int PageSize = 9;

    public EventsController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? city, string? province, string? eventType,
        DateOnly? dateFrom, DateOnly? dateTo, int page = 1)
    {
        var filter = new EventFilter
        {
            City = city,
            Province = province,
            EventType = eventType,
            DateFrom = dateFrom ?? DateOnly.FromDateTime(DateTime.Today),   // "upcoming" by default
            DateTo = dateTo,
            Status = "Published",
            Page = Math.Max(1, page),
            PageSize = PageSize
        };

        var result = await Api.GetEventsAsync(filter);
        if (!result.Success) return await LoadFailedAsync(result);

        return View(new EventsIndexViewModel
        {
            Filter = filter,
            Events = result.Data!.Items,
            TotalCount = result.Data.TotalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var result = await Api.GetEventAsync(id);
        if (!result.Success) return await LoadFailedAsync(result);

        // Which categories is this participant already enrolled in? (Not critical, so ignore a failure)
        var mine = await Api.GetMyEnrolmentsAsync();
        var enrolled = mine.Success
            ? mine.Data!.Where(e => e.Status != "Cancelled").Select(e => e.CategoryId).ToHashSet()
            : new HashSet<int>();

        return View(new EventDetailsViewModel { Event = result.Data!, EnrolledCategoryIds = enrolled });
    }

    [HttpPost("{id:int}/enrol")]
    public async Task<IActionResult> Enrol(int id, int categoryId, string? notes)
    {
        if (categoryId <= 0)
        {
            AlertError("Please choose a category before enrolling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await Api.EnrolAsync(categoryId, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
        if (!result.Success) return await ActionFailedAsync(result, nameof(Details), null, new { id });

        AlertSuccess($"You're in! You are enrolled for {result.Data!.CategoryName} at {result.Data.EventTitle}.");
        return RedirectToAction("Index", "MyEnrolments");
    }
}
