using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Participants enrol into categories and the enrolment is correctly recorded.</summary>
public class EnrolmentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public EnrolmentTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_AndTheEnrolmentIsRecordedCorrectly()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);

        var response = await participant.Client.PostAsJsonAsync("/api/enrolments",
            new { categoryId = category.CategoryId, notes = "First 10km" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<EnrolmentDto>();
        Assert.Equal(participant.UserId, dto!.ParticipantId);
        Assert.Equal(category.CategoryId, dto.CategoryId);
        Assert.Equal(ev.EventId, dto.EventId);

        // Check what is really in the database: participant + category (and so event) are linked
        var stored = await _factory.WithDb(db => db.Enrolments
            .Include(e => e.Category)
            .SingleAsync(e => e.EnrolmentId == dto.EnrolmentId));
        Assert.Equal(participant.UserId, stored.ParticipantId);
        Assert.Equal(category.CategoryId, stored.CategoryId);
        Assert.Equal(ev.EventId, stored.Category!.EventId);
        Assert.Equal("Confirmed", stored.Status);
        Assert.Equal("Pending", stored.PaymentStatus);   // entry fee > 0
        Assert.Equal("First 10km", stored.Notes);
    }

    [Fact]
    public async Task EnrolInFreeCategory_IsMarkedAsPaid()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId, fee: 0);

        var enrolment = await _factory.Enrol(participant, category.CategoryId);

        Assert.Equal("Paid", enrolment.PaymentStatus);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);

        var response = await organiser.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_InTheSameCategory_Returns409()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        await _factory.Enrol(participant, category.CategoryId);

        var second = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_InCategoryThatDoesNotExist_Returns404()
    {
        var participant = await _factory.NewParticipant();
        var response = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_Returns409()
    {
        var organiser = await _factory.NewOrganiser();
        var first = await _factory.NewParticipant();
        var second = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId, maxEntries: 1);
        await _factory.Enrol(first, category.CategoryId);

        var response = await second.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenEventIsFull_Returns409()
    {
        var organiser = await _factory.NewOrganiser();
        var first = await _factory.NewParticipant();
        var second = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser, maxParticipants: 1);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        await _factory.Enrol(first, category.CategoryId);

        var response = await second.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InDraftEvent_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser, "Draft");
        var category = await _factory.NewCategory(organiser, ev.EventId);

        var response = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenTooYoungForCategory_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var teenager = await _factory.NewParticipant(new DateOnly(2015, 1, 1));
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId, ageMin: 18);

        var response = await teenager.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOnlyTheirOwnEnrolments()
    {
        var organiser = await _factory.NewOrganiser();
        var me = await _factory.NewParticipant();
        var someoneElse = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var mine = await _factory.Enrol(me, category.CategoryId);
        var theirs = await _factory.Enrol(someoneElse, category.CategoryId);

        var list = await me.Client.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/me");

        Assert.Contains(list!, e => e.EnrolmentId == mine.EnrolmentId);
        Assert.DoesNotContain(list!, e => e.EnrolmentId == theirs.EnrolmentId);
        Assert.All(list!, e => Assert.Equal(me.UserId, e.ParticipantId));
    }

    [Fact]
    public async Task Participant_CannotViewSomeoneElsesEnrolment_Returns403()
    {
        var organiser = await _factory.NewOrganiser();
        var owner = await _factory.NewParticipant();
        var snoop = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var enrolment = await _factory.Enrol(owner, category.CategoryId);

        var response = await snoop.Client.GetAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForTheirEvent_WithParticipantInfo()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var enrolment = await _factory.Enrol(participant, category.CategoryId);

        var list = await organiser.Client.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventId}/enrolments");

        var item = Assert.Single(list!);
        Assert.Equal(enrolment.EnrolmentId, item.EnrolmentId);
        Assert.Equal(participant.Email, item.Participant!.Email);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsOfAnotherOrganisersEvent_Returns403()
    {
        var owner = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(owner);

        var response = await other.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_AndEnrolAgainAfterwards()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId, maxEntries: 1);
        var enrolment = await _factory.Enrol(participant, category.CategoryId);

        var cancel = await participant.Client.PutAsync($"/api/enrolments/{enrolment.EnrolmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<EnrolmentDto>())!.Status);

        // The place was freed up, so the same participant can enrol again
        var again = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanAssignBibNumber_ButNotDuplicateWithinAnEvent()
    {
        var organiser = await _factory.NewOrganiser();
        var p1 = await _factory.NewParticipant();
        var p2 = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var e1 = await _factory.Enrol(p1, category.CategoryId);
        var e2 = await _factory.Enrol(p2, category.CategoryId);

        var ok = await organiser.Client.PutAsJsonAsync($"/api/enrolments/{e1.EnrolmentId}/bib", new { bibNumber = "A-100" });
        var duplicate = await organiser.Client.PutAsJsonAsync($"/api/enrolments/{e2.EnrolmentId}/bib", new { bibNumber = "A-100" });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("A-100", (await ok.Content.ReadFromJsonAsync<EnrolmentDto>())!.BibNumber);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
