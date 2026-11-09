using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>User registration, login, session handling and profile.</summary>
public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AuthTests(ApiFactory factory) => _factory = factory;

    private static object ValidRegistration(string email, string role = "Participant", string password = "Passw0rd!123") => new
    {
        firstName = "Ayanda", lastName = "Nkosi", email, password, role,
        phoneNumber = "0821112222", dateOfBirth = "1992-04-10"
    };

    [Fact]
    public async Task Register_WithValidDetails_Returns201_AndNeverReturnsThePassword()
    {
        var client = _factory.CreateClient();
        var email = $"new-{Guid.NewGuid():N}@test.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Passw0rd!123", body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("Participant", user!.Role);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task Register_AsOrganiser_AssignsOrganiserRole()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration($"org-{Guid.NewGuid():N}@test.com", "organiser"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("Organiser", user!.Role);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData("1")]
    public async Task Register_WithInvalidRole_Returns400(string role)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration($"bad-{Guid.NewGuid():N}@test.com", role));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration($"short-{Guid.NewGuid():N}@test.com", password: "abc"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration("not-an-email"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@test.com";

        var first = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email.ToUpper()));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_NotThePlainOne()
    {
        var client = _factory.CreateClient();
        var email = $"hash-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email));

        var stored = await _factory.WithDb(db => db.Users.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync());

        Assert.NotEqual("Passw0rd!123", stored);
        Assert.DoesNotContain("Passw0rd!123", stored);
        Assert.True(stored.Length > 30);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200_AndUserDetails()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email, "Organiser"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("Organiser", login!.User.Role);
        Assert.True(login.ExpiresIn > 0);
        Assert.Contains(response.Headers, h => h.Key == "Set-Cookie");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"wrong-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", ValidRegistration(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.com", password = "Passw0rd!123" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLoggingIn_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLogin_ReturnsTheLoggedInUser()
    {
        var user = await _factory.NewParticipant();

        var response = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.UserId, profile!.UserId);
        Assert.Equal("Participant", profile.Role);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var user = await _factory.NewParticipant();
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/users/me")).StatusCode);

        var logout = await user.Client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndPhone_ButNotEmailOrRole()
    {
        var user = await _factory.NewParticipant();

        var response = await user.Client.PutAsJsonAsync("/api/users/me", new
        {
            firstName = "Updated", lastName = "Person", phoneNumber = "0710000000", dateOfBirth = "1988-08-08"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("Updated", profile!.FirstName);
        Assert.Equal("0710000000", profile.PhoneNumber);
        Assert.Equal(user.Email, profile.Email);
        Assert.Equal("Participant", profile.Role);
    }

    [Fact]
    public async Task UpdateProfile_WithoutLoggingIn_Returns401()
    {
        var response = await _factory.CreateClient().PutAsJsonAsync("/api/users/me",
            new { firstName = "A", lastName = "B" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsPublic()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
