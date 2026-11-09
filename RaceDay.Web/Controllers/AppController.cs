using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared behaviour for every controller: API access, alerts and consistent failure handling.</summary>
public abstract class AppController : Controller
{
    protected AppController(IRaceDayApi api) => Api = api;

    protected IRaceDayApi Api { get; }

    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    protected bool IsOrganiser => User.IsInRole("Organiser");

    protected void AlertSuccess(string message) => TempData["Success"] = message;
    protected void AlertError(string message) => TempData["Error"] = message;

    protected IActionResult RedirectToRoleHome() =>
        IsOrganiser ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Index", "Events");

    /// <summary>The user's API session ended: sign them out and send them to the login page.</summary>
    protected async Task<IActionResult> SessionExpiredAsync()
    {
        await HttpContext.SignOutAsync();
        HttpContext.Session.Clear();
        AlertError("Your session has expired. Please log in again.");
        return RedirectToAction("Login", "Account");
    }

    /// <summary>A page that needed data from the API could not be shown. Never shows raw errors.</summary>
    protected async Task<IActionResult> LoadFailedAsync(ApiResult result)
    {
        if (result.IsUnauthorized) return await SessionExpiredAsync();
        if (result.IsForbidden) return RedirectToAction("AccessDenied", "Account");

        if (result.IsNotFound)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("~/Views/Shared/NotFound.cshtml");
        }

        Response.StatusCode = result.Unreachable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        return View("~/Views/Shared/ServiceProblem.cshtml", result.Message);
    }

    /// <summary>A form submission failed in the API: show the reason on the page the user can safely go back to.</summary>
    protected async Task<IActionResult> ActionFailedAsync(ApiResult result, string action, string? controller = null, object? routeValues = null)
    {
        if (result.IsUnauthorized) return await SessionExpiredAsync();
        if (result.IsForbidden) return RedirectToAction("AccessDenied", "Account");

        AlertError(result.Message);
        return RedirectToAction(action, controller, routeValues);
    }

    /// <summary>
    /// Copies validation messages from the API onto the form. Messages for a field the form has
    /// show next to that field; anything else shows in the summary at the top.
    /// </summary>
    protected void AddApiErrors<TModel>(ApiResult result)
    {
        var added = false;
        foreach (var (key, messages) in result.Errors)
        {
            var property = key.Length > 0 ? char.ToUpperInvariant(key[0]) + key[1..] : key;
            var known = typeof(TModel).GetProperty(property) is not null;
            foreach (var message in messages)
            {
                ModelState.AddModelError(known ? property : "", message);
                added = true;
            }
        }
        if (!added) ModelState.AddModelError("", result.Message);
    }
}
