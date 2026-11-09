using System.Globalization;

namespace RaceDay.Web.Helpers;

/// <summary>Small presentation helpers used by the views (formatting only, no business rules).</summary>
public static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>1 -> "1st", 2 -> "2nd", 11 -> "11th", 47 -> "47th".</summary>
    public static string Ordinal(int n)
    {
        var lastTwo = n % 100;
        if (lastTwo is >= 11 and <= 13) return n + "th";
        return (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };
    }

    /// <summary>01:42:35 -> "1:42:35".</summary>
    public static string Duration(TimeSpan? time) =>
        time is null ? "-" : time.Value.ToString(@"h\:mm\:ss", Culture);

    /// <summary>Value for a text box, e.g. "01:42:35".</summary>
    public static string DurationInput(TimeSpan? time) =>
        time is null ? "" : time.Value.ToString(@"hh\:mm\:ss", Culture);

    public static string Date(DateOnly date) => date.ToString("ddd, d MMM yyyy", Culture);
    public static string ShortDate(DateOnly date) => date.ToString("d MMM yyyy", Culture);
    public static string Day(DateOnly date) => date.ToString("dd", Culture);
    public static string Month(DateOnly date) => date.ToString("MMM", Culture).ToUpperInvariant();

    public static string Time(TimeOnly? time) => time is null ? "" : time.Value.ToString("HH:mm", Culture);

    public static string Distance(decimal? km) => km is null ? "-" : km.Value.ToString("0.##", Culture) + " km";

    public static string Money(decimal amount) => amount == 0 ? "Free" : "R" + amount.ToString("0.##", Culture);

    public static string Initials(string first, string last)
    {
        var a = string.IsNullOrWhiteSpace(first) ? "" : first.Trim()[0].ToString();
        var b = string.IsNullOrWhiteSpace(last) ? "" : last.Trim()[0].ToString();
        return (a + b).ToUpperInvariant();
    }

    /// <summary>CSS class that gives a status its colour (see site.css).</summary>
    public static string StatusClass(string? status) => "status-" + (status ?? "unknown").ToLowerInvariant();

    public static string TypeEmoji(string? eventType) => eventType switch
    {
        "Walk" => "\U0001F6B6",   // walking
        "Cycle" => "\U0001F6B4",  // cycling
        _ => "\U0001F3C3"         // running
    };

    /// <summary>Turns the API image path "/api/images/events/x.jpg" into the MVC path "/media/events/x.jpg".</summary>
    public static string? MediaUrl(string? apiPath)
    {
        const string prefix = "/api/images/";
        return apiPath is not null && apiPath.StartsWith(prefix, StringComparison.Ordinal)
            ? "/media/" + apiPath[prefix.Length..]
            : null;
    }
}

/// <summary>Fixed lists used by drop-downs.</summary>
public static class Lookups
{
    public static readonly string[] Provinces =
    {
        "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo",
        "Mpumalanga", "Northern Cape", "North West", "Western Cape"
    };

    public static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };
    public static readonly string[] GenderRestrictions = { "None", "Male", "Female" };
    public static readonly string[] ResultStatuses = { "Finished", "DNF", "DNS", "DQ" };
    public static readonly string[] CreateEventStatuses = { "Draft", "Published" };
    public static readonly string[] EditEventStatuses = { "Draft", "Published", "Cancelled", "Completed" };
}
