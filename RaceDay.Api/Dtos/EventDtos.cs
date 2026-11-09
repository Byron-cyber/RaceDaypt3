using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Run, Walk or Cycle.</summary>
    public string EventType { get; set; } = "Run";

    /// <summary>Headline distance of the event in km.</summary>
    public decimal? DistanceKm { get; set; }

    public int? MaxParticipants { get; set; }

    /// <summary>Path of the banner image served by this API (GET /api/images/...). Null if no banner.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Number of active (not cancelled) enrolments. Only filled in for GET /api/events/my.</summary>
    public int? EnrolmentCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CreateEventRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Format YYYY-MM-DD.</summary>
    [Required] public DateOnly? EventDate { get; set; }

    /// <summary>Format HH:mm.</summary>
    public TimeOnly? StartTime { get; set; }

    [Required, StringLength(300)] public string Location { get; set; } = string.Empty;
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Province { get; set; } = string.Empty;

    /// <summary>"Draft" (default) or "Published".</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>"Run" (default), "Walk" or "Cycle".</summary>
    public string EventType { get; set; } = "Run";

    /// <summary>Headline distance in km (optional).</summary>
    [Range(0.01, 100000)] public decimal? DistanceKm { get; set; }

    [Range(1, int.MaxValue)] public int? MaxParticipants { get; set; }
}

/// <summary>All fields optional: only the fields you send are changed.</summary>
public class UpdateEventRequest
{
    [StringLength(200)] public string? Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    [StringLength(300)] public string? Location { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(50)] public string? Province { get; set; }

    /// <summary>Draft, Published, Cancelled or Completed.</summary>
    public string? Status { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    public string? EventType { get; set; }

    [Range(0.01, 100000)] public decimal? DistanceKm { get; set; }
    [Range(1, int.MaxValue)] public int? MaxParticipants { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string GenderRestriction { get; set; } = string.Empty;
    public int? MaxEntries { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CategoryDetailDto : CategoryDto
{
    public EventDto? Event { get; set; }
}

public class CreateCategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>Distance in kilometres, must be greater than 0.</summary>
    [Range(0.01, 100000)] public decimal DistanceKm { get; set; }

    [Range(0, 1000000)] public decimal EntryFee { get; set; }
    [Range(0, 120)] public int? AgeMin { get; set; }
    [Range(0, 120)] public int? AgeMax { get; set; }

    /// <summary>None (default), Male or Female.</summary>
    public string GenderRestriction { get; set; } = "None";

    [Range(1, int.MaxValue)] public int? MaxEntries { get; set; }
}

/// <summary>All fields optional: only the fields you send are changed.</summary>
public class UpdateCategoryRequest
{
    [StringLength(100)] public string? Name { get; set; }
    [Range(0.01, 100000)] public decimal? DistanceKm { get; set; }
    [Range(0, 1000000)] public decimal? EntryFee { get; set; }
    [Range(0, 120)] public int? AgeMin { get; set; }
    [Range(0, 120)] public int? AgeMax { get; set; }
    public string? GenderRestriction { get; set; }
    [Range(1, int.MaxValue)] public int? MaxEntries { get; set; }
}
