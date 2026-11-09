using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>
/// Role-based access: unauthenticated users get 401, and the wrong role gets 403.
/// The role check happens before any data is looked up, so the ids used here don't need to exist.
/// </summary>
public class RoleAccessTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public RoleAccessTests(ApiFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string url) =>
        client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });

    public static IEnumerable<object[]> OrganiserOnly => new[]
    {
        new object[] { "POST", "/api/events" },
        new object[] { "PUT", "/api/events/1" },
        new object[] { "DELETE", "/api/events/1" },
        new object[] { "GET", "/api/events/my" },
        new object[] { "POST", "/api/events/1/categories" },
        new object[] { "PUT", "/api/categories/1" },
        new object[] { "DELETE", "/api/categories/1" },
        new object[] { "GET", "/api/events/1/enrolments" },
        new object[] { "PUT", "/api/enrolments/1/bib" },
        new object[] { "POST", "/api/results" },
        new object[] { "PUT", "/api/results/1" },
        new object[] { "DELETE", "/api/results/1" },
        new object[] { "GET", "/api/users/1" },
    };

    public static IEnumerable<object[]> ParticipantOnly => new[]
    {
        new object[] { "POST", "/api/enrolments" },
        new object[] { "GET", "/api/enrolments/me" },
        new object[] { "GET", "/api/results/me" },
    };

    [Theory]
    [MemberData(nameof(OrganiserOnly))]
    public async Task OrganiserEndpoints_RejectParticipants_With403(string method, string url)
    {
        var participant = await _factory.NewParticipant();
        var response = await Send(participant.Client, method, url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ParticipantOnly))]
    public async Task ParticipantEndpoints_RejectOrganisers_With403(string method, string url)
    {
        var organiser = await _factory.NewOrganiser();
        var response = await Send(organiser.Client, method, url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OrganiserOnly))]
    [MemberData(nameof(ParticipantOnly))]
    public async Task ProtectedEndpoints_RejectAnonymousUsers_With401(string method, string url)
    {
        var response = await Send(_factory.CreateClient(), method, url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/enrolments/1")]
    [InlineData("/api/results/1")]
    public async Task AuthenticatedOnlyEndpoints_RejectAnonymousUsers_With401(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/health")]
    public async Task PublicEndpoints_WorkWithoutLoggingIn(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
