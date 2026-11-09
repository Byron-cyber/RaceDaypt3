using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>A participant's personal race history.</summary>
[Authorize(Roles = "Participant")]
[Route("my-results")]
public class MyResultsController : AppController
{
    public MyResultsController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyResultsAsync();
        if (!result.Success) return await LoadFailedAsync(result);
        return View(result.Data!.OrderByDescending(r => r.EventDate).ToList());
    }
}
