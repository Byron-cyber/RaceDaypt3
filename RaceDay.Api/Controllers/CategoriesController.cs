using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age / distance categories within an event (e.g. 10km, 21km, Under 20).</summary>
[Tags("Categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public CategoriesController(ApplicationDbContext db) => _db = db;

    /// <summary>List the categories of an event (everyone).</summary>
    /// <returns>200 with an array of categories, 404 if the event does not exist.</returns>
    [HttpGet("events/{eventId:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventCategories(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null || !CanSee(ev)) return NotFoundError("Event not found.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm)
            .ToListAsync();
        return Ok(categories.Select(c => c.ToDto()).ToList());
    }

    /// <summary>Get one category together with its parent event (everyone).</summary>
    /// <returns>200 with the category and event, 404 if it does not exist.</returns>
    [HttpGet("categories/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CategoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _db.Categories.AsNoTracking().Include(c => c.Event)
            .SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category?.Event is null || !CanSee(category.Event)) return NotFoundError("Category not found.");
        return Ok(category.ToDetailDto());
    }

    /// <summary>Add a category to an event you own (Organisers only).</summary>
    /// <remarks>
    /// Expects name and distanceKm (greater than 0). Optional: entryFee, ageMin, ageMax,
    /// genderRestriction (None, Male, Female) and maxEntries.
    /// </remarks>
    /// <returns>201 with the category, 400 for invalid data, 403 if not your event, 404 if the event does not exist.</returns>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CreateCategoryRequest request)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundError("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only add categories to your own events.");

        var gender = GenderRestrictions.All.FirstOrDefault(g =>
            string.Equals(g, request.GenderRestriction?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (gender is null) return BadRequestError("genderRestriction must be None, Male or Female.", "genderRestriction");
        if (request.AgeMin is not null && request.AgeMax is not null && request.AgeMin > request.AgeMax)
            return BadRequestError("ageMin cannot be greater than ageMax.", "ageMin");

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            DistanceKm = request.DistanceKm,
            EntryFee = request.EntryFee,
            AgeMin = request.AgeMin,
            AgeMax = request.AgeMax,
            GenderRestriction = gender,
            MaxEntries = request.MaxEntries
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, category.ToDto());
    }

    /// <summary>Update a category of an event you own. Only the fields you send are changed.</summary>
    /// <returns>200 with the updated category, 400 for invalid data, 403 if not your event, 404 if it does not exist.</returns>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundError("Category not found.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenError("You can only change categories of your own events.");

        if (request.GenderRestriction is not null)
        {
            var gender = GenderRestrictions.All.FirstOrDefault(g =>
                string.Equals(g, request.GenderRestriction.Trim(), StringComparison.OrdinalIgnoreCase));
            if (gender is null) return BadRequestError("genderRestriction must be None, Male or Female.", "genderRestriction");
            category.GenderRestriction = gender;
        }
        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequestError("name cannot be empty.", "name");
            category.Name = request.Name.Trim();
        }
        if (request.DistanceKm is not null) category.DistanceKm = request.DistanceKm.Value;
        if (request.EntryFee is not null) category.EntryFee = request.EntryFee.Value;
        if (request.AgeMin is not null) category.AgeMin = request.AgeMin;
        if (request.AgeMax is not null) category.AgeMax = request.AgeMax;
        if (request.MaxEntries is not null) category.MaxEntries = request.MaxEntries;

        if (category.AgeMin is not null && category.AgeMax is not null && category.AgeMin > category.AgeMax)
            return BadRequestError("ageMin cannot be greater than ageMax.", "ageMin");

        await _db.SaveChangesAsync();
        return Ok(category.ToDto());
    }

    /// <summary>Delete a category of an event you own, only if nobody has enrolled in it.</summary>
    /// <returns>204 when deleted, 403 if not your event, 404 if it does not exist, 409 if it already has enrolments.</returns>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundError("Category not found.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenError("You can only delete categories of your own events.");

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return ConflictError("This category has enrolments and cannot be deleted.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private bool CanSee(Event ev) =>
        ev.Status != EventStatuses.Draft || (IsAuthenticated && ev.OrganiserId == CurrentUserId);
}
