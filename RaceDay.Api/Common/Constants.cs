namespace RaceDay.Api.Common;

/// <summary>The two roles in the system (see Roles table in Part 1).</summary>
public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public static class EventStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
    public static readonly string[] All = { Draft, Published, Cancelled, Completed };
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
}

public static class GenderRestrictions
{
    public static readonly string[] All = { "None", "Male", "Female" };
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
}

public static class ResultStatuses
{
    public const string Finished = "Finished";
    public static readonly string[] All = { "Finished", "DNF", "DNS", "DQ" };
}
