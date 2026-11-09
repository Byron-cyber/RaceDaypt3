namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";

    /// <summary>Run, Walk or Cycle (Part 3 addition).</summary>
    public string EventType { get; set; } = "Run";

    /// <summary>Headline distance of the event in km (Part 3 addition). Each category also has its own distance.</summary>
    public decimal? DistanceKm { get; set; }
    public int? MaxParticipants { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
