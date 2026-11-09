using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Only filled in for organiser views.</summary>
    public UserSummaryDto? Participant { get; set; }
}

public class CreateEnrolmentRequest
{
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public class AssignBibRequest
{
    [Required, StringLength(20)] public string BibNumber { get; set; } = string.Empty;
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = string.Empty;
    public TimeSpan? ChipTime { get; set; }
    public TimeSpan? GunTime { get; set; }
    public DateTime CapturedAt { get; set; }

    /// <summary>How many participants finished the whole event (used to show e.g. "47th of 312").</summary>
    public int TotalFinishers { get; set; }

    public DateOnly EventDate { get; set; }
}

public class CreateResultRequest
{
    [Range(1, int.MaxValue)] public int EnrolmentId { get; set; }

    /// <summary>Format HH:mm:ss. Required when Status is "Finished".</summary>
    public TimeSpan? FinishTime { get; set; }

    [Range(1, int.MaxValue)] public int? OverallPosition { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryPosition { get; set; }

    /// <summary>Finished (default), DNF, DNS or DQ.</summary>
    public string Status { get; set; } = "Finished";

    public TimeSpan? ChipTime { get; set; }
    public TimeSpan? GunTime { get; set; }
}

/// <summary>All fields optional: only the fields you send are changed.</summary>
public class UpdateResultRequest
{
    public TimeSpan? FinishTime { get; set; }
    [Range(1, int.MaxValue)] public int? OverallPosition { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryPosition { get; set; }
    public string? Status { get; set; }
    public TimeSpan? ChipTime { get; set; }
    public TimeSpan? GunTime { get; set; }
}
