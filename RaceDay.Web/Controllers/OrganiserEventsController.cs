using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organisers create, edit and cancel their events (including the banner image).</summary>
[Authorize(Roles = "Organiser")]
[Route("organiser/events")]
public class OrganiserEventsController : AppController
{
    public OrganiserEventsController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) return await LoadFailedAsync(result);
        return View(result.Data!.OrderByDescending(e => e.EventDate).ToList());
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new EventFormViewModel { EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) });

    [HttpPost("create")]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await Api.CreateEventAsync(model.ToRequest());
        if (!result.Success)
        {
            if (result.IsUnauthorized) return await SessionExpiredAsync();
            AddApiErrors<EventFormViewModel>(result);
            return View("Form", model);
        }

        // The banner is uploaded through the API, which stores it in Azure Blob Storage
        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadBannerAsync(result.Data!.EventId, model.Banner);
            if (!upload.Success)
            {
                AlertError($"Your event was created, but the banner could not be uploaded: {upload.Message}");
                return RedirectToAction(nameof(Edit), new { id = result.Data.EventId });
            }
        }

        AlertSuccess($"\"{result.Data!.Title}\" has been created. Next, add its categories.");
        return RedirectToAction("Manage", "OrganiserCategories", new { eventId = result.Data.EventId });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await Api.GetEventAsync(id);
        if (!result.Success) return await LoadFailedAsync(result);
        if (result.Data!.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        return View("Form", EventFormViewModel.From(result.Data));
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await Api.UpdateEventAsync(id, model.ToRequest());
        if (!result.Success)
        {
            if (result.IsUnauthorized) return await SessionExpiredAsync();
            if (result.IsForbidden) return RedirectToAction("AccessDenied", "Account");
            AddApiErrors<EventFormViewModel>(result);
            return View("Form", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadBannerAsync(id, model.Banner);
            if (!upload.Success)
            {
                AlertError($"Your changes were saved, but the banner could not be uploaded: {upload.Message}");
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        AlertSuccess("Your event has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await Api.CancelEventAsync(id);
        if (!result.Success) return await ActionFailedAsync(result, nameof(Index));

        AlertSuccess("The event has been cancelled.");
        return RedirectToAction(nameof(Index));
    }
}
