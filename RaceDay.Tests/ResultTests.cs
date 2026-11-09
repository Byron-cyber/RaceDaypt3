using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Organisers capture results; participants can only see their own.</summary>
public class ResultTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ResultTests(ApiFactory factory) => _factory = factory;

    private async Task<(TestUser organiser, TestUser participant, EventDto ev, EnrolmentDto enrolment)> Setup()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var enrolment = await _factory.Enrol(participant, category.CategoryId);
        return (organiser, participant, ev, enrolment);
    }

    private static Task<HttpResponseMessage> Publish(TestUser organiser, int eventId) =>
        organiser.Client.PutAsJsonAsync($"/api/events/{eventId}", new { status = "Completed" });

    private static object FinishedResult(int enrolmentId, int position = 1) => new
    {
        enrolmentId, finishTime = "00:48:30", overallPosition = position, categoryPosition = position,
        status = "Finished", chipTime = "00:48:25", gunTime = "00:48:30"
    };

    [Fact]
    public async Task Organiser_CanCaptureResult_AndEnrolmentBecomesCompleted()
    {
        var (organiser, _, _, enrolment) = await Setup();

        var response = await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(new TimeSpan(0, 48, 30), result!.FinishTime);
        Assert.Equal(1, result.OverallPosition);

        var stored = await _factory.WithDb(db => db.Results.SingleAsync(r => r.ResultId == result.ResultId));
        Assert.Equal(organiser.UserId, stored.CapturedByUserId);
        var storedEnrolment = await _factory.WithDb(db => db.Enrolments.SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId));
        Assert.Equal("Completed", storedEnrolment.Status);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_Returns403()
    {
        var (_, participant, _, enrolment) = await Setup();
        var response = await participant.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, _, enrolment) = await Setup();
        var stranger = await _factory.NewOrganiser();

        var response = await stranger.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForMissingEnrolment_Returns404()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(999999));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_ForTheSameEnrolment_Returns409()
    {
        var (organiser, _, _, enrolment) = await Setup();
        await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId));

        var second = await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId, 2));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureFinishedResult_WithoutFinishTime_Returns400()
    {
        var (organiser, _, _, enrolment) = await Setup();

        var response = await organiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureDnfResult_WithoutFinishTime_IsAllowed()
    {
        var (organiser, _, _, enrolment) = await Setup();

        var response = await organiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, status = "DNF" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_WithInvalidStatus_Returns400()
    {
        var (organiser, _, _, enrolment) = await Setup();

        var response = await organiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, status = "Winner", finishTime = "00:30:00" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnResults_ButNotOthers()
    {
        var (organiser, participant, ev, enrolment) = await Setup();
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var other = await _factory.NewParticipant();
        var otherEnrolment = await _factory.Enrol(other, category.CategoryId);

        var mine = await (await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId, 1)))
            .Content.ReadFromJsonAsync<ResultDto>();
        var theirs = await (await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(otherEnrolment.EnrolmentId, 2)))
            .Content.ReadFromJsonAsync<ResultDto>();

        await Publish(organiser, ev.EventId);

        var list = await participant.Client.GetFromJsonAsync<List<ResultDto>>("/api/results/me");
        Assert.Contains(list!, r => r.ResultId == mine!.ResultId);
        Assert.DoesNotContain(list!, r => r.ResultId == theirs!.ResultId);

        Assert.Equal(HttpStatusCode.OK, (await participant.Client.GetAsync($"/api/results/{mine!.ResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.Client.GetAsync($"/api/results/{theirs!.ResultId}")).StatusCode);
    }

    [Fact]
    public async Task EventResults_ArePublic_AndOrderedByPosition()
    {
        var (organiser, _, ev, enrolment) = await Setup();
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var runnerUp = await _factory.NewParticipant();
        var runnerUpEnrolment = await _factory.Enrol(runnerUp, category.CategoryId);

        await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(runnerUpEnrolment.EnrolmentId, 2));
        await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId, 1));
        await Publish(organiser, ev.EventId);

        var list = await _factory.CreateClient().GetFromJsonAsync<List<ResultDto>>($"/api/events/{ev.EventId}/results");

        Assert.Equal(2, list!.Count);
        Assert.Equal(new[] { 1, 2 }, list.Select(r => r.OverallPosition!.Value).ToArray());
    }

    [Fact]
    public async Task Results_AreHiddenFromEveryoneButTheOrganiser_UntilPublished()
    {
        var (organiser, participant, ev, enrolment) = await Setup();
        var created = await (await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultDto>();

        // Not published yet
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/results")).StatusCode);
        Assert.Empty(await participant.Client.GetFromJsonAsync<List<ResultDto>>("/api/results/me") ?? new());
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.Client.GetAsync($"/api/results/{created!.ResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await organiser.Client.GetAsync($"/api/events/{ev.EventId}/results")).StatusCode);

        // After publishing
        await Publish(organiser, ev.EventId);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/results")).StatusCode);
        Assert.Single(await participant.Client.GetFromJsonAsync<List<ResultDto>>("/api/results/me") ?? new());
    }

    [Fact]
    public async Task MyResults_IncludeEventDetails_AndTheTotalNumberOfFinishers()
    {
        var (organiser, participant, ev, enrolment) = await Setup();
        var category = await _factory.NewCategory(organiser, ev.EventId);
        var other = await _factory.NewParticipant();
        var otherEnrolment = await _factory.Enrol(other, category.CategoryId);
        await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId, 1));
        await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(otherEnrolment.EnrolmentId, 2));
        await Publish(organiser, ev.EventId);

        var list = await participant.Client.GetFromJsonAsync<List<ResultDto>>("/api/results/me");

        var mine = Assert.Single(list!);
        Assert.Equal(ev.Title, mine.EventTitle);
        Assert.Equal(1, mine.OverallPosition);
        Assert.Equal(2, mine.TotalFinishers);
        Assert.Equal(new TimeSpan(0, 48, 30), mine.FinishTime);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var (organiser, _, _, enrolment) = await Setup();
        var created = await (await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultDto>();

        var response = await organiser.Client.PutAsJsonAsync($"/api/results/{created!.ResultId}",
            new { finishTime = "00:47:59", overallPosition = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(new TimeSpan(0, 47, 59), updated!.FinishTime);
        Assert.Equal(3, updated.OverallPosition);
    }

    [Fact]
    public async Task Organiser_CanDeleteAResult_AndTheEnrolmentGoesBackToConfirmed()
    {
        var (organiser, _, _, enrolment) = await Setup();
        var created = await (await organiser.Client.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultDto>();

        var response = await organiser.Client.DeleteAsync($"/api/results/{created!.ResultId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var storedEnrolment = await _factory.WithDb(db => db.Enrolments.SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId));
        Assert.Equal("Confirmed", storedEnrolment.Status);
    }
}
