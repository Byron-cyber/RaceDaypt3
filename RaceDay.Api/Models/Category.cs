namespace RaceDay.Api.Models;

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string GenderRestriction { get; set; } = "None";
    public int? MaxEntries { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
