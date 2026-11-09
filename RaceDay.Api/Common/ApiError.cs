namespace RaceDay.Api.Common;

/// <summary>Consistent error shape for every endpoint: { "message": "...", "errors": { ... } }.</summary>
public class ApiError
{
    public string Message { get; set; } = string.Empty;
    public IDictionary<string, string[]>? Errors { get; set; }
}
