namespace RaceDay.Web.Services;

/// <summary>The outcome of one call to the RaceDay API, with a message that is safe to show to users.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }

    /// <summary>A friendly message. Never a stack trace or raw response body.</summary>
    public string Message { get; init; } = "";

    /// <summary>Field-level validation messages from the API (field name -> messages).</summary>
    public IDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>True when the API could not be reached at all.</summary>
    public bool Unreachable { get; init; }

    public bool IsUnauthorized => StatusCode == 401;
    public bool IsForbidden => StatusCode == 403;
    public bool IsNotFound => StatusCode == 404;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}
