using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Register, log in and log out through the RaceDay API.</summary>
public class AccountController : AppController
{
    public AccountController(IRaceDayApi api) : base(api) { }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await Api.LoginAsync(model.Email, model.Password);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            model.Password = "";
            return View(model);
        }

        // Remember who the user is and which role they have for every following page
        var user = result.Data!.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Role, user.Role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        AlertSuccess($"Welcome back, {user.FirstName}!");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return user.Role == "Organiser"
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "Events");
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await Api.RegisterAsync(new RegisterRequest
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            Password = model.Password,
            Role = model.Role,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth
        });

        if (!result.Success)
        {
            AddApiErrors<RegisterViewModel>(result);
            model.Password = "";
            model.ConfirmPassword = "";
            return View(model);
        }

        AlertSuccess("Your account has been created. Log in to get started!");
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Ends the API session and the MVC session, then goes back to the login page.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await Api.LogoutAsync();
        await HttpContext.SignOutAsync();
        HttpContext.Session.Clear();

        AlertSuccess("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
