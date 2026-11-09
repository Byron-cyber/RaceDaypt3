using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event management: organisers can manage their events, everyone can view them.</summary>
public class EventTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public EventTests(ApiFactory factory) => _factory = factory;

    private static object NewEventBody(string status = "Draft") => new
    {
        title = "Midweek 5km", description = "Easy run", eventDate = "2027-02-10", startTime = "17:30",
        location = "Zoo Lake", city = "Johannesburg", province = "Gauteng", status, maxParticipants = 200
    };

    [Fact]
    public async Task Organiser_CanCreateEvent_AndBecomesItsOwner()
    {
        var organiser = await _factory.NewOrganiser();

        var response = await organiser.Client.PostAsJsonAsync("/api/events", NewEventBody("Published"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal(organiser.UserId, created!.OrganiserId);
        Assert.Equal("Midweek 5km", created.Title);
        Assert.Equal("Published", created.Status);
        Assert.Equal(new TimeOnly(17, 30), created.StartTime);

        var stored = await _factory.WithDb(db => db.Events.SingleAsync(e => e.EventId == created.EventId));
        Assert.Equal(organiser.UserId, stored.OrganiserId);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var participant = await _factory.NewParticipant();
        var response = await participant.Client.PostAsJsonAsync("/api/events", NewEventBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events", NewEventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingTitle_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            eventDate = "2027-02-10", location = "Zoo Lake", city = "Johannesburg", province = "Gauteng"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("message", error);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidStatus_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PostAsJsonAsync("/api/events", NewEventBody("Completed"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent_PartiallyAndFully()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}",
            new { title = "Renamed Race", city = "Durban" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Renamed Race", updated!.Title);
        Assert.Equal("Durban", updated.City);
        Assert.Equal(ev.Location, updated.Location);   // untouched field stays the same
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(owner);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", new { title = "Hijacked" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await _factory.WithDb(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.Equal(ev.Title, stored.Title);
    }

    [Fact]
    public async Task UpdateEvent_ThatDoesNotExist_Returns404()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PutAsJsonAsync("/api/events/999999", new { title = "x" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletingOwnEvent_CancelsIt()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        var response = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await _factory.WithDb(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.Equal("Cancelled", stored.Status);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(owner);

        var response = await other.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await _factory.WithDb(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.Equal("Published", stored.Status);
    }

    [Fact]
    public async Task Participant_CanViewPublishedEvents_AndSingleEvent()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        await _factory.NewCategory(organiser, ev.EventId);

        var list = await participant.Client.GetFromJsonAsync<List<EventDto>>("/api/events");
        Assert.Contains(list!, e => e.EventId == ev.EventId);

        var detail = await participant.Client.GetFromJsonAsync<EventDetailDto>($"/api/events/{ev.EventId}");
        Assert.Equal(ev.EventId, detail!.EventId);
        Assert.Single(detail.Categories);
    }

    [Fact]
    public async Task PublicEventList_NeverIncludesDraftEvents()
    {
        var organiser = await _factory.NewOrganiser();
        var published = await _factory.NewEvent(organiser, "Published");
        var draft = await _factory.NewEvent(organiser, "Draft");

        var list = await _factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events?pageSize=100");

        Assert.Contains(list!, e => e.EventId == published.EventId);
        Assert.DoesNotContain(list!, e => e.EventId == draft.EventId);
    }

    [Fact]
    public async Task DraftEvent_IsHiddenFromThePublic_ButVisibleToItsOwner()
    {
        var organiser = await _factory.NewOrganiser();
        var draft = await _factory.NewEvent(organiser, "Draft");

        var anonymous = await _factory.CreateClient().GetAsync($"/api/events/{draft.EventId}");
        var owner = await organiser.Client.GetAsync($"/api/events/{draft.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
    }

    [Fact]
    public async Task MyEvents_ListsOnlyTheOrganisersOwnEvents_IncludingDrafts()
    {
        var organiser = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var mine = await _factory.NewEvent(organiser, "Draft");
        var theirs = await _factory.NewEvent(other, "Published");

        var list = await organiser.Client.GetFromJsonAsync<List<EventDto>>("/api/events/my");

        Assert.Contains(list!, e => e.EventId == mine.EventId);
        Assert.DoesNotContain(list!, e => e.EventId == theirs.EventId);
    }

    [Fact]
    public async Task EventList_CanBeFilteredByCity()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);   // city = Pretoria

        var inPretoria = await _factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events?city=pretoria&pageSize=100");
        var inDurban = await _factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events?city=Durbanville&pageSize=100");

        Assert.Contains(inPretoria!, e => e.EventId == ev.EventId);
        Assert.DoesNotContain(inDurban!, e => e.EventId == ev.EventId);
    }

    [Fact]
    public async Task CreateEvent_StoresEventTypeAndDistance()
    {
        var organiser = await _factory.NewOrganiser();

        var response = await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            title = "Tour", eventDate = "2027-04-10", location = "Park", city = "Cape Town", province = "Western Cape",
            eventType = "cycle", distanceKm = 109.5, status = "Published"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Cycle", created!.EventType);
        Assert.Equal(109.5m, created.DistanceKm);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            title = "Swim", eventDate = "2027-04-10", location = "Pool", city = "Durban", province = "KwaZulu-Natal", eventType = "Swim"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EventList_CanBeFilteredByEventType()
    {
        var organiser = await _factory.NewOrganiser();
        var walk = await (await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            title = "Walk", eventDate = "2027-04-10", location = "Park", city = "Durban", province = "KwaZulu-Natal",
            eventType = "Walk", status = "Published"
        })).Content.ReadFromJsonAsync<EventDto>();
        var run = await _factory.NewEvent(organiser);   // default type = Run

        var walks = await _factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events?eventType=Walk&pageSize=100");

        Assert.Contains(walks!, e => e.EventId == walk!.EventId);
        Assert.DoesNotContain(walks!, e => e.EventId == run.EventId);
    }

    [Fact]
    public async Task MyEvents_IncludesTheNumberOfActiveEnrolments()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var p1 = await _factory.NewParticipant();
        var p2 = await _factory.NewParticipant();
        await _factory.Enrol(p1, category.CategoryId);
        var second = await _factory.Enrol(p2, category.CategoryId);
        await p2.Client.PutAsync($"/api/enrolments/{second.EnrolmentId}/cancel", null);   // cancelled ones don't count

        var list = await organiser.Client.GetFromJsonAsync<List<EventDto>>("/api/events/my");

        Assert.Equal(1, list!.Single(e => e.EventId == ev.EventId).EnrolmentCount);
    }

    [Fact]
    public async Task GetEvent_ThatDoesNotExist_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
