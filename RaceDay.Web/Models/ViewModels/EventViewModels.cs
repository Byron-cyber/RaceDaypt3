using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models.ViewModels;

// ---------------- Participant ----------------

public class EventsIndexViewModel
{
    public EventFilter Filter { get; set; } = new();
    public List<EventDto> Events { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Filter.PageSize));
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

// ---------------- Organiser ----------------

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public DateOnly Today { get; set; }

    public int TotalEvents => Events.Count;
    public int PublishedEvents => Events.Count(e => e.Status == "Published");
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount ?? 0);

    public List<EventDto> Upcoming => Events
        .Where(e => e.EventDate >= Today && e.Status is "Published" or "Draft")
        .OrderBy(e => e.EventDate)
        .Take(5)
        .ToList();
}

public class EventPickerViewModel
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string ButtonText { get; set; } = "";
    public string Action { get; set; } = "";
    public string Controller { get; set; } = "";
    public List<EventDto> Events { get; set; } = new();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(200)]
    [Display(Name = "Event name")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Please describe the event.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [Display(Name = "Date")]
    public DateOnly? EventDate { get; set; }

    [Display(Name = "Start time")]
    public TimeOnly? StartTime { get; set; }

    [Required(ErrorMessage = "Please enter the venue or location."), StringLength(300)]
    public string Location { get; set; } = "";

    [Required(ErrorMessage = "Please enter the city."), StringLength(100)]
    public string City { get; set; } = "";

    [Required(ErrorMessage = "Please choose the province.")]
    public string Province { get; set; } = "";

    [Required(ErrorMessage = "Please choose the event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Required(ErrorMessage = "Please enter the distance in kilometres.")]
    [Range(0.01, 100000, ErrorMessage = "The distance must be more than 0 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required]
    public string Status { get; set; } = "Draft";

    [Range(1, 1000000, ErrorMessage = "Maximum participants must be at least 1.")]
    [Display(Name = "Maximum participants")]
    public int? MaxParticipants { get; set; }

    // Banner
    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }
    public string? ExistingImageUrl { get; set; }

    public SaveEventRequest ToRequest() => new()
    {
        Title = Title,
        Description = Description,
        EventDate = EventDate,
        StartTime = StartTime,
        Location = Location,
        City = City,
        Province = Province,
        Status = Status,
        EventType = EventType,
        DistanceKm = DistanceKm,
        MaxParticipants = MaxParticipants
    };

    public static EventFormViewModel From(EventDto e) => new()
    {
        EventId = e.EventId,
        Title = e.Title,
        Description = e.Description,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        Location = e.Location,
        City = e.City,
        Province = e.Province,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        Status = e.Status,
        MaxParticipants = e.MaxParticipants,
        ExistingImageUrl = e.ImageUrl
    };
}

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please name the category (e.g. 10km or Under 20)."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Please enter the distance in kilometres.")]
    [Range(0.01, 100000, ErrorMessage = "The distance must be more than 0 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please enter the entry fee (0 for free).")]
    [Range(0, 1000000, ErrorMessage = "The entry fee cannot be negative.")]
    [Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; } = 0;

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? AgeMin { get; set; }

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? AgeMax { get; set; }

    [Display(Name = "Gender")]
    public string GenderRestriction { get; set; } = "None";

    [Range(1, 1000000, ErrorMessage = "Maximum entries must be at least 1.")]
    [Display(Name = "Maximum entries")]
    public int? MaxEntries { get; set; }

    public SaveCategoryRequest ToRequest() => new()
    {
        Name = Name,
        DistanceKm = DistanceKm,
        EntryFee = EntryFee,
        AgeMin = AgeMin,
        AgeMax = AgeMax,
        GenderRestriction = GenderRestriction,
        MaxEntries = MaxEntries
    };

    public static CategoryFormViewModel From(CategoryDto c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee,
        AgeMin = c.AgeMin,
        AgeMax = c.AgeMax,
        GenderRestriction = c.GenderRestriction,
        MaxEntries = c.MaxEntries
    };
}

public class CategoryManageViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EnrolmentsManageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public EnrolmentDto Enrolment { get; set; } = new();
    public ResultDto? Result { get; set; }
}

public class ResultsManageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<ResultRow> Rows { get; set; } = new();
    public bool IsPublished => Event.Status == "Completed";
}

/// <summary>One row of the results-entry table, as posted by its form.</summary>
public class ResultInputModel
{
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "Finished";
}
