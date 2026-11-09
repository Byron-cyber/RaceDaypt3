using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>A logged-in test user with their own HttpClient (each client keeps its own session cookie).</summary>
public record TestUser(HttpClient Client, int UserId, string Email, string Role);

/// <summary>
/// Starts the real API in memory and swaps SQL Server for an in-memory database,
/// so the tests need no database and run the same locally and in GitHub Actions.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Passw0rd!123";
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();
    private int _counter;

    /// <summary>Fake image storage used instead of Azure Blob Storage.</summary>
    public FakeBlobStorageService Blobs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server registration of the DbContext
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                (d.ServiceType.IsGenericType && d.ServiceType.GenericTypeArguments.Contains(typeof(ApplicationDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));

            // Swap Azure Blob Storage for the in-memory fake
            foreach (var d in services.Where(d => d.ServiceType == typeof(IBlobStorageService)).ToList())
                services.Remove(d);
            services.AddSingleton<IBlobStorageService>(Blobs);
        });
    }

    /// <summary>Run code directly against the test database (for checking what was really stored).</summary>
    public async Task<T> WithDb<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>Registers a brand new user with the given role and logs them in.</summary>
    public async Task<TestUser> NewUser(string role, DateOnly? dateOfBirth = null)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"{role.ToLower()}{n}-{Guid.NewGuid():N}@test.com";
        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test", lastName = role + n, email, password = Password, role,
            dateOfBirth = dateOfBirth ?? new DateOnly(1990, 1, 1)
        });
        register.EnsureSuccessStatusCode();
        var user = (await register.Content.ReadFromJsonAsync<UserDto>())!;

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        return new TestUser(client, user.UserId, email, role);
    }

    public Task<TestUser> NewOrganiser() => NewUser("Organiser");
    public Task<TestUser> NewParticipant(DateOnly? dob = null) => NewUser("Participant", dob);

    public async Task<EventDto> NewEvent(TestUser organiser, string status = "Published", int? maxParticipants = null)
    {
        var response = await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            title = "Test Race " + Guid.NewGuid().ToString("N")[..6],
            description = "A race for testing",
            eventDate = "2027-06-01",
            startTime = "06:00",
            location = "Test Park",
            city = "Pretoria",
            province = "Gauteng",
            status,
            maxParticipants
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public async Task<CategoryDto> NewCategory(TestUser organiser, int eventId,
        int? maxEntries = null, int? ageMin = null, int? ageMax = null, decimal fee = 100m)
    {
        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "10km", distanceKm = 10, entryFee = fee, ageMin, ageMax,
            genderRestriction = "None", maxEntries
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    public async Task<EnrolmentDto> Enrol(TestUser participant, int categoryId)
    {
        var response = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
    }
}
