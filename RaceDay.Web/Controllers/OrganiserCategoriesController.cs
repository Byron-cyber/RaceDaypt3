using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Age and distance categories for each of an organiser's events.</summary>
[Authorize(Roles = "Organiser")]
[Route("organiser")]
public class OrganiserCategoriesController : AppController
{
    public OrganiserCategoriesController(IRaceDayApi api) : base(api) { }

    /// <summary>Pick which event to manage categories for.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) return await LoadFailedAsync(result);

        return View("~/Views/Shared/EventPicker.cshtml", new EventPickerViewModel
        {
            Title = "Categories",
            Subtitle = "Choose an event to add or change its age and distance categories.",
            ButtonText = "Manage categories",
            Action = "Manage",
            Controller = "OrganiserCategories",
            Events = result.Data!.OrderBy(e => e.EventDate).ToList()
        });
    }

    [HttpGet("events/{eventId:int}/categories")]
    public async Task<IActionResult> Manage(int eventId)
    {
        var result = await Api.GetEventAsync(eventId);
        if (!result.Success) return await LoadFailedAsync(result);
        if (result.Data!.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        return View(new CategoryManageViewModel
        {
            Event = result.Data,
            NewCategory = new CategoryFormViewModel { EventId = eventId }
        });
    }

    [HttpPost("events/{eventId:int}/categories")]
    public async Task<IActionResult> Create(int eventId, [Bind(Prefix = "NewCategory")] CategoryFormViewModel model)
    {
        model.EventId = eventId;
        if (!ModelState.IsValid)
        {
            var page = await Api.GetEventAsync(eventId);
            if (!page.Success) return await LoadFailedAsync(page);
            return View("Manage", new CategoryManageViewModel { Event = page.Data!, NewCategory = model });
        }

        var result = await Api.CreateCategoryAsync(eventId, model.ToRequest());
        if (!result.Success) return await ShowApiErrorsOnManageAsync(eventId, model, result);

        AlertSuccess($"Category \"{result.Data!.Name}\" added.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    [HttpGet("events/{eventId:int}/categories/{id:int}/edit")]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        var result = await Api.GetCategoryAsync(id);
        if (!result.Success) return await LoadFailedAsync(result);
        if (result.Data!.EventId != eventId) return NotFound();

        var page = await Api.GetEventAsync(eventId);
        if (!page.Success) return await LoadFailedAsync(page);
        if (page.Data!.OrganiserId != CurrentUserId) return RedirectToAction("AccessDenied", "Account");

        ViewData["EventTitle"] = page.Data.Title;
        return View(CategoryFormViewModel.From(result.Data));
    }

    [HttpPost("events/{eventId:int}/categories/{id:int}/edit")]
    public async Task<IActionResult> Edit(int eventId, int id, CategoryFormViewModel model)
    {
        model.EventId = eventId;
        model.CategoryId = id;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.UpdateCategoryAsync(id, model.ToRequest());
        if (!result.Success)
        {
            if (result.IsUnauthorized) return await SessionExpiredAsync();
            if (result.IsForbidden) return RedirectToAction("AccessDenied", "Account");
            AddApiErrors<CategoryFormViewModel>(result);
            return View(model);
        }

        AlertSuccess("Category updated.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    [HttpPost("events/{eventId:int}/categories/{id:int}/delete")]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await Api.DeleteCategoryAsync(id);
        if (!result.Success) return await ActionFailedAsync(result, nameof(Manage), null, new { eventId });

        AlertSuccess("Category deleted.");
        return RedirectToAction(nameof(Manage), new { eventId });
    }

    private async Task<IActionResult> ShowApiErrorsOnManageAsync(int eventId, CategoryFormViewModel model, ApiResult failure)
    {
        if (failure.IsUnauthorized) return await SessionExpiredAsync();
        if (failure.IsForbidden) return RedirectToAction("AccessDenied", "Account");

        // Field names from the API are plain ("distanceKm"), the form fields are prefixed ("NewCategory.DistanceKm")
        foreach (var (key, messages) in failure.Errors)
        {
            var property = key.Length > 0 ? char.ToUpperInvariant(key[0]) + key[1..] : key;
            var known = typeof(CategoryFormViewModel).GetProperty(property) is not null;
            foreach (var message in messages)
                ModelState.AddModelError(known ? "NewCategory." + property : "", message);
        }
        if (failure.Errors.Count == 0) ModelState.AddModelError("", failure.Message);

        var page = await Api.GetEventAsync(eventId);
        if (!page.Success) return await LoadFailedAsync(page);
        return View("Manage", new CategoryManageViewModel { Event = page.Data!, NewCategory = model });
    }
}
