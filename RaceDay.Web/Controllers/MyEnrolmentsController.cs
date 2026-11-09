using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>A participant's own enrolments and their statuses.</summary>
[Authorize(Roles = "Participant")]
[Route("my-enrolments")]
public class MyEnrolmentsController : AppController
{
    public MyEnrolmentsController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEnrolmentsAsync();
        if (!result.Success) return await LoadFailedAsync(result);
        return View(result.Data!.OrderBy(e => e.EventDate).ToList());
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await Api.CancelEnrolmentAsync(id);
        if (!result.Success) return await ActionFailedAsync(result, nameof(Index));

        AlertSuccess("Your enrolment has been cancelled.");
        return RedirectToAction(nameof(Index));
    }
}
