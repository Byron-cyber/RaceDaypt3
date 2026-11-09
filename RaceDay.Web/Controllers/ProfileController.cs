using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>View and update the logged-in user's profile and profile picture.</summary>
[Authorize]
public class ProfileController : AppController
{
    public ProfileController(IRaceDayApi api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyProfileAsync();
        if (!result.Success) return await LoadFailedAsync(result);
        return View(ToViewModel(result.Data!));
    }

    [HttpPost]
    public async Task<IActionResult> Update(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await FillReadOnlyDetailsAsync(model);
            return View("Index", model);
        }

        var result = await Api.UpdateMyProfileAsync(new UpdateProfileRequest
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth
        });

        if (!result.Success)
        {
            if (result.IsUnauthorized) return await SessionExpiredAsync();
            AddApiErrors<ProfileViewModel>(result);
            await FillReadOnlyDetailsAsync(model);
            return View("Index", model);
        }

        AlertSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Picture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            AlertError("Please choose a picture to upload.");
            return RedirectToAction(nameof(Index));
        }

        var result = await Api.UploadProfilePictureAsync(picture);
        if (!result.Success) return await ActionFailedAsync(result, nameof(Index));

        AlertSuccess("Your profile picture has been updated.");
        return RedirectToAction(nameof(Index));
    }

    private async Task FillReadOnlyDetailsAsync(ProfileViewModel model)
    {
        var current = await Api.GetMyProfileAsync();
        if (!current.Success) return;
        model.Email = current.Data!.Email;
        model.Role = current.Data.Role;
        model.ProfilePictureUrl = current.Data.ProfilePictureUrl;
        model.CreatedAt = current.Data.CreatedAt;
    }

    private static ProfileViewModel ToViewModel(UserDto user) => new()
    {
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhoneNumber = user.PhoneNumber,
        DateOfBirth = user.DateOfBirth,
        Email = user.Email,
        Role = user.Role,
        ProfilePictureUrl = user.ProfilePictureUrl,
        CreatedAt = user.CreatedAt
    };
}
