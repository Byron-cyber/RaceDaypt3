namespace RaceDay.Api.Models;

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public User? Participant { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Confirmed";
    public string? BibNumber { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? Notes { get; set; }

    public Result? Result { get; set; }
}
