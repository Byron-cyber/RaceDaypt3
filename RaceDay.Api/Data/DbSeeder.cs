using Microsoft.AspNetCore.Identity;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Inserts demo data (same people and events as the Part 1 SQL script) the first time the API runs in
/// Development, so you can try every endpoint straight away. Every demo user has the password: Password123!
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static void Seed(ApplicationDbContext db, IPasswordHasher<User> hasher)
    {
        if (db.Users.Any()) return;

        User NewUser(int roleId, string first, string last, string email, string phone, DateOnly dob)
        {
            var u = new User { RoleId = roleId, FirstName = first, LastName = last, Email = email, PhoneNumber = phone, DateOfBirth = dob };
            u.PasswordHash = hasher.HashPassword(u, DemoPassword);
            return u;
        }

        var thabo = NewUser(1, "Thabo", "Mokoena", "thabo.mokoena@raceday.co.za", "0821234567", new DateOnly(1985, 3, 12));
        var sarah = NewUser(1, "Sarah", "van der Berg", "sarah.vdberg@capetowncycle.org", "0839876543", new DateOnly(1978, 7, 22));
        var lerato = NewUser(2, "Lerato", "Dlamini", "lerato.dlamini@gmail.com", "0712345678", new DateOnly(1995, 11, 5));
        var johan = NewUser(2, "Johan", "Pretorius", "johan.pretorius@outlook.com", "0845678901", new DateOnly(1990, 1, 30));
        db.Users.AddRange(thabo, sarah, lerato, johan);
        db.SaveChanges();

        // Upcoming events are relative to today so the demo data always has events to browse
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var soweto = new Event
        {
            OrganiserId = thabo.UserId, Title = "Soweto Marathon 2026",
            Description = "The iconic Soweto Marathon through the historic streets of Soweto.",
            EventDate = today.AddDays(45), StartTime = new TimeOnly(6, 0),
            Location = "FNB Stadium, Soccer City", City = "Johannesburg", Province = "Gauteng",
            Status = "Published", EventType = "Run", DistanceKm = 42.2m, MaxParticipants = 15000
        };
        var twoOceans = new Event
        {
            OrganiserId = thabo.UserId, Title = "Two Oceans Training Run",
            Description = "Supported training run for athletes preparing for the Two Oceans Marathon.",
            EventDate = new DateOnly(2026, 3, 15), StartTime = new TimeOnly(5, 30),
            Location = "University of Cape Town", City = "Cape Town", Province = "Western Cape",
            Status = "Completed", EventType = "Run", DistanceKm = 56m, MaxParticipants = 500
        };
        var cycleTour = new Event
        {
            OrganiserId = sarah.UserId, Title = "Cape Town Cycle Tour Fun Ride",
            Description = "A fun non-competitive cycle ride finishing on Sea Point promenade.",
            EventDate = new DateOnly(2026, 3, 8), StartTime = new TimeOnly(7, 0),
            Location = "Cape Town Stadium", City = "Cape Town", Province = "Western Cape",
            Status = "Completed", EventType = "Cycle", DistanceKm = 109m, MaxParticipants = 8000
        };
        var walk = new Event
        {
            OrganiserId = sarah.UserId, Title = "Sunset Fun Walk",
            Description = "A relaxed community walk around Zoo Lake. Bring the whole family.",
            EventDate = today.AddDays(20), StartTime = new TimeOnly(17, 0),
            Location = "Zoo Lake", City = "Johannesburg", Province = "Gauteng",
            Status = "Published", EventType = "Walk", DistanceKm = 5m, MaxParticipants = 1000
        };
        var ride = new Event
        {
            OrganiserId = thabo.UserId, Title = "Pretoria Winter Cycle Classic",
            Description = "A scenic road ride through Pretoria with a challenging climb to the finish.",
            EventDate = today.AddDays(75), StartTime = new TimeOnly(6, 30),
            Location = "Loftus Versfeld", City = "Pretoria", Province = "Gauteng",
            Status = "Published", EventType = "Cycle", DistanceKm = 60m, MaxParticipants = 2500
        };
        db.Events.AddRange(soweto, twoOceans, cycleTour, walk, ride);
        db.SaveChanges();

        Category Cat(Event e, string name, decimal km, decimal fee, int? ageMin, int max) =>
            new() { EventId = e.EventId, Name = name, DistanceKm = km, EntryFee = fee, AgeMin = ageMin, GenderRestriction = "None", MaxEntries = max };

        var full = Cat(soweto, "Full Marathon", 42.20m, 450m, 18, 5000);
        var half = Cat(soweto, "Half Marathon", 21.10m, 350m, 16, 6000);
        var tenK = Cat(soweto, "10km Road Race", 10m, 200m, 14, 4000);
        var ultra = Cat(twoOceans, "56km Ultra Simulation", 56m, 150m, 20, 200);
        var run21 = Cat(twoOceans, "21km Supported Run", 21.10m, 100m, 16, 300);
        var classic = Cat(cycleTour, "109km Classic", 109m, 0m, 18, 3000);
        var fun42 = Cat(cycleTour, "42km Fun Ride", 42m, 0m, 12, 5000);
        var walk5 = Cat(walk, "5km Fun Walk", 5m, 50m, null, 1000);
        var ride60 = Cat(ride, "60km Classic", 60m, 250m, 16, 1500);
        var ride30 = Cat(ride, "30km Social Ride", 30m, 150m, 12, 1000);
        db.Categories.AddRange(full, half, tenK, ultra, run21, classic, fun42, walk5, ride60, ride30);
        db.SaveChanges();

        var e1 = new Enrolment { ParticipantId = lerato.UserId, CategoryId = half.CategoryId, Status = "Confirmed", BibNumber = "H-1024", PaymentStatus = "Paid", Notes = "First half marathon" };
        var e2 = new Enrolment { ParticipantId = lerato.UserId, CategoryId = run21.CategoryId, Status = "Completed", BibNumber = "T-045", PaymentStatus = "Paid" };
        var e3 = new Enrolment { ParticipantId = johan.UserId, CategoryId = full.CategoryId, Status = "Confirmed", BibNumber = "M-0881", PaymentStatus = "Paid", Notes = "Comrades qualifier" };
        var e4 = new Enrolment { ParticipantId = johan.UserId, CategoryId = classic.CategoryId, Status = "Completed", BibNumber = "C-3321", PaymentStatus = "Paid" };
        var e5 = new Enrolment { ParticipantId = lerato.UserId, CategoryId = fun42.CategoryId, Status = "Confirmed", BibNumber = "C-5102", PaymentStatus = "Paid" };
        db.Enrolments.AddRange(e1, e2, e3, e4, e5);
        db.SaveChanges();

        // Results only for the two events that have already been run
        db.Results.AddRange(
            new Result
            {
                EnrolmentId = e2.EnrolmentId, FinishTime = new TimeSpan(2, 5, 10), OverallPosition = 145, CategoryPosition = 32,
                Status = "Finished", ChipTime = new TimeSpan(2, 5, 3), GunTime = new TimeSpan(2, 5, 10), CapturedByUserId = thabo.UserId
            },
            new Result
            {
                EnrolmentId = e4.EnrolmentId, FinishTime = new TimeSpan(3, 58, 12), OverallPosition = 892, CategoryPosition = 210,
                Status = "Finished", ChipTime = new TimeSpan(3, 57, 55), GunTime = new TimeSpan(3, 58, 12), CapturedByUserId = sarah.UserId
            });
        db.SaveChanges();
    }
}
