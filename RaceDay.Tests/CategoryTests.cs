using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class CategoryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public CategoryTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanAddCategoryToOwnEvent_AndEveryoneCanViewIt()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new
        {
            name = "Under 20 5km", distanceKm = 5, entryFee = 50, ageMax = 19, genderRestriction = "None", maxEntries = 100
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CategoryDto>();

        var list = await _factory.CreateClient().GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventId}/categories");
        Assert.Contains(list!, c => c.CategoryId == created!.CategoryId);

        var detail = await _factory.CreateClient().GetFromJsonAsync<CategoryDetailDto>($"/api/categories/{created!.CategoryId}");
        Assert.Equal(ev.EventId, detail!.Event!.EventId);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToSomeoneElsesEvent_Returns403()
    {
        var owner = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(owner);

        var response = await other.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Sneaky", distanceKm = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ToMissingEvent_Returns404()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PostAsJsonAsync("/api/events/999999/categories", new { name = "X", distanceKm = 5 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task AddCategory_WithZeroOrNegativeDistance_Returns400(decimal distance)
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Bad", distanceKm = distance });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_WithAgeMinAboveAgeMax_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Odd", distanceKm = 5, ageMin = 40, ageMax = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnCategory()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);

        var response = await organiser.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { name = "10km Road Race", entryFee = 250 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("10km Road Race", updated!.Name);
        Assert.Equal(250m, updated.EntryFee);
    }

    [Fact]
    public async Task Organiser_CannotUpdateSomeoneElsesCategory_Returns403()
    {
        var owner = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(owner);
        var category = await _factory.NewCategory(owner, ev.EventId);

        var response = await other.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}", new { name = "Mine now" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteCategoryWithoutEnrolments()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/categories/{category.CategoryId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_Returns409()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        await _factory.Enrol(participant, category.CategoryId);

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
