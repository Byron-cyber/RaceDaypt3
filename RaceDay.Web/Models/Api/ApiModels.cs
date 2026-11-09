namespace RaceDay.Web.Models.Api;

// These classes only describe the JSON the RaceDay API sends and receives.
// They hold no business logic: the rules live in the API.

public class UserDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserSummaryDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class LoginResponse
{
    public int ExpiresIn { get; set; }
    public UserDto User { get; set; } = new();
}

public class RegisterRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}

public class UpdateProfileRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public string Location { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string Status { get; set; } = "";
    public string EventType { get; set; } = "Run";
    public decimal? DistanceKm { get; set; }
    public int? MaxParticipants { get; set; }
    public string? ImageUrl { get; set; }
    public int? EnrolmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

public class SaveEventRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public string? Location { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Status { get; set; }
    public string? EventType { get; set; }
    public decimal? DistanceKm { get; set; }
    public int? MaxParticipants { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string GenderRestriction { get; set; } = "None";
    public int? MaxEntries { get; set; }
}

public class SaveCategoryRequest
{
    public string? Name { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal? EntryFee { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string? GenderRestriction { get; set; }
    public int? MaxEntries { get; set; }
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = "";
    public string? BibNumber { get; set; }
    public string PaymentStatus { get; set; } = "";
    public string? Notes { get; set; }
    public UserSummaryDto? Participant { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = "";
    public string? BibNumber { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "";
    public TimeSpan? ChipTime { get; set; }
    public TimeSpan? GunTime { get; set; }
    public int TotalFinishers { get; set; }
}

public class SaveResultRequest
{
    public int? EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string? Status { get; set; }
}

/// <summary>Filters for browsing events.</summary>
public class EventFilter
{
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? EventType { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public class PagedList<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class ApiImage
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}
