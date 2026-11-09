using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    public HomeController(IRaceDayApi api) : base(api) { }

    /// <summary>Public landing page. Logged-in users go straight to the home page of their role.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();

        // A small taste of what's coming up. If the API is down the page still works without it.
        var filter = new EventFilter
        {
            DateFrom = DateOnly.FromDateTime(DateTime.Today),
            Status = "Published",
            PageSize = 3
        };
        var upcoming = await Api.GetEventsAsync(filter);
        return View(upcoming.Success ? upcoming.Data!.Items : new List<EventDto>());
    }

    /// <summary>Friendly error pages for every status code and for unexpected errors.</summary>
    [Route("error/{code?}")]
    [AcceptVerbs("GET", "POST")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        var status = code ?? 500;
        Response.StatusCode = status;
        return status == 404 ? View("NotFound") : View("Error", status);
    }
}
