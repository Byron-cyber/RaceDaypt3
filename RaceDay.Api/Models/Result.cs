namespace RaceDay.Api.Models;

/// <summary>Maps to the Results table from Part 1 (one result per enrolment).</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "Finished";
    public TimeSpan? ChipTime { get; set; }
    public TimeSpan? GunTime { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public int CapturedByUserId { get; set; }
    public User? CapturedBy { get; set; }
}
