using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Common;

/// <summary>Converts database entities to DTOs so password hashes and navigation loops never leave the API.</summary>
public static class Mappers
{
    public static UserDto ToDto(this User u) => new()
    {
        UserId = u.UserId,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Role = u.Role?.RoleName ?? string.Empty,
        PhoneNumber = u.PhoneNumber,
        DateOfBirth = u.DateOfBirth,
        ProfilePictureUrl = u.ProfilePictureUrl,
        CreatedAt = u.CreatedAt
    };

    public static UserSummaryDto ToSummary(this User u) => new()
    {
        UserId = u.UserId,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Role = u.Role?.RoleName ?? string.Empty
    };

    public static EventDto ToDto(this Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        Title = e.Title,
        Description = e.Description,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        Location = e.Location,
        City = e.City,
        Province = e.Province,
        Status = e.Status,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        MaxParticipants = e.MaxParticipants,
        ImageUrl = e.ImageUrl,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    public static EventDetailDto ToDetailDto(this Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        Title = e.Title,
        Description = e.Description,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        Location = e.Location,
        City = e.City,
        Province = e.Province,
        Status = e.Status,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        MaxParticipants = e.MaxParticipants,
        ImageUrl = e.ImageUrl,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        Categories = e.Categories.OrderBy(c => c.DistanceKm).Select(c => c.ToDto()).ToList()
    };

    public static CategoryDto ToDto(this Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee,
        AgeMin = c.AgeMin,
        AgeMax = c.AgeMax,
        GenderRestriction = c.GenderRestriction,
        MaxEntries = c.MaxEntries,
        CreatedAt = c.CreatedAt
    };

    public static CategoryDetailDto ToDetailDto(this Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee,
        AgeMin = c.AgeMin,
        AgeMax = c.AgeMax,
        GenderRestriction = c.GenderRestriction,
        MaxEntries = c.MaxEntries,
        CreatedAt = c.CreatedAt,
        Event = c.Event?.ToDto()
    };

    public static EnrolmentDto ToDto(this Enrolment e, bool includeParticipant = false) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        CategoryId = e.CategoryId,
        CategoryName = e.Category?.Name ?? string.Empty,
        DistanceKm = e.Category?.DistanceKm ?? 0,
        EventId = e.Category?.EventId ?? 0,
        EventTitle = e.Category?.Event?.Title ?? string.Empty,
        EventDate = e.Category?.Event?.EventDate ?? default,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status,
        BibNumber = e.BibNumber,
        PaymentStatus = e.PaymentStatus,
        Notes = e.Notes,
        Participant = includeParticipant && e.Participant is not null ? e.Participant.ToSummary() : null
    };

    public static ResultDto ToDto(this Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        EventId = r.Enrolment?.Category?.EventId ?? 0,
        EventTitle = r.Enrolment?.Category?.Event?.Title ?? string.Empty,
        CategoryId = r.Enrolment?.CategoryId ?? 0,
        CategoryName = r.Enrolment?.Category?.Name ?? string.Empty,
        ParticipantId = r.Enrolment?.ParticipantId ?? 0,
        ParticipantName = r.Enrolment?.Participant is null
            ? string.Empty
            : $"{r.Enrolment.Participant.FirstName} {r.Enrolment.Participant.LastName}",
        BibNumber = r.Enrolment?.BibNumber,
        FinishTime = r.FinishTime,
        OverallPosition = r.OverallPosition,
        CategoryPosition = r.CategoryPosition,
        Status = r.Status,
        ChipTime = r.ChipTime,
        GunTime = r.GunTime,
        CapturedAt = r.CapturedAt,
        EventDate = r.Enrolment?.Category?.Event?.EventDate ?? default
    };
}
