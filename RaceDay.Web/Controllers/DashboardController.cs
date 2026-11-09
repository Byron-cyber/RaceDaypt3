using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser home: a summary of their events, enrolments and upcoming dates.</summary>
[Authorize(Roles = "Organiser")]
[Route("organiser")]
public class DashboardController : AppController
{
    public DashboardController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) return await LoadFailedAsync(result);

        return View(new DashboardViewModel
        {
            Events = result.Data!,
            Today = DateOnly.FromDateTime(DateTime.Today)
        });
    }
}
