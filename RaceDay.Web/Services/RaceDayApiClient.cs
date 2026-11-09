using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// Talks to the RaceDay API over HTTP. It is the only way this MVC app reads or writes data.
/// The API session cookie of the logged-in user is kept in the MVC server-side session and sent with every call.
/// </summary>
public class RaceDayApiClient : IRaceDayApi
{
    public const string SessionKey = "raceday-api-session";
    private const string ApiCookieName = "RaceDay.Session";
    private const string UnreachableMessage = "RaceDay can't reach its server right now. Please try again in a moment.";
    private const string GenericMessage = "We couldn't complete that request. Please try again.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ILogger<RaceDayApiClient> _logger;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor context, ILogger<RaceDayApiClient> logger)
    {
        _http = http;
        _context = context;
        _logger = logger;
    }

    private ISession? Session => _context.HttpContext?.Session;

    // =====================================================================
    // Authentication
    // =====================================================================

    public async Task<ApiResult<LoginResponse>> LoginAsync(string email, string password)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonContent.Create(new { email, password }, options: Json)
            };
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return await FailureAsync<LoginResponse>(response);

            var cookie = ExtractSessionCookie(response);
            if (cookie is null || Session is null)
                return Failed<LoginResponse>(500, GenericMessage);

            Session.SetString(SessionKey, cookie);   // remember the API session for this user
            var data = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
            return new ApiResult<LoginResponse> { Success = true, StatusCode = 200, Data = data };
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            _logger.LogWarning(ex, "Login call to the API failed");
            return Unreachable<LoginResponse>();
        }
    }

    public Task<ApiResult<UserDto>> RegisterAsync(RegisterRequest request) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register", Body(request));

    public async Task LogoutAsync()
    {
        await SendPlainAsync(HttpMethod.Post, "api/auth/logout");
        Session?.Remove(SessionKey);
    }

    // =====================================================================
    // Profile
    // =====================================================================

    public Task<ApiResult<UserDto>> GetMyProfileAsync() =>
        SendAsync<UserDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserDto>> UpdateMyProfileAsync(UpdateProfileRequest request) =>
        SendAsync<UserDto>(HttpMethod.Put, "api/users/me", Body(request));

    public Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/users/me/profile-picture", FileContent(file));

    // =====================================================================
    // Events
    // =====================================================================

    public async Task<ApiResult<PagedList<EventDto>>> GetEventsAsync(EventFilter filter)
    {
        var query = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
        Add("city", filter.City);
        Add("province", filter.Province);
        Add("eventType", filter.EventType);
        Add("status", filter.Status);
        Add("dateFrom", filter.DateFrom?.ToString("yyyy-MM-dd"));
        Add("dateTo", filter.DateTo?.ToString("yyyy-MM-dd"));
        query.Add($"page={filter.Page}");
        query.Add($"pageSize={filter.PageSize}");

        try
        {
            using var request = NewRequest(HttpMethod.Get, "api/events?" + string.Join("&", query));
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return await FailureAsync<PagedList<EventDto>>(response);

            var items = await response.Content.ReadFromJsonAsync<List<EventDto>>(Json) ?? new List<EventDto>();
            var total = items.Count;
            if (response.Headers.TryGetValues("X-Total-Count", out var values) && int.TryParse(values.FirstOrDefault(), out var parsed))
                total = parsed;

            return new ApiResult<PagedList<EventDto>>
            {
                Success = true, StatusCode = 200,
                Data = new PagedList<EventDto> { Items = items, TotalCount = total }
            };
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            _logger.LogWarning(ex, "Could not load events from the API");
            return Unreachable<PagedList<EventDto>>();
        }
    }

    public Task<ApiResult<EventDetailDto>> GetEventAsync(int id) =>
        SendAsync<EventDetailDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<List<EventDto>>> GetMyEventsAsync() =>
        SendAsync<List<EventDto>>(HttpMethod.Get, "api/events/my");

    public Task<ApiResult<EventDto>> CreateEventAsync(SaveEventRequest request) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", Body(request));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, SaveEventRequest request) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", Body(request));

    public Task<ApiResult> CancelEventAsync(int id) =>
        SendPlainAsync(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        SendAsync<EventDto>(HttpMethod.Post, $"api/events/{eventId}/banner", FileContent(file));

    // =====================================================================
    // Categories
    // =====================================================================

    public Task<ApiResult<List<CategoryDto>>> GetCategoriesAsync(int eventId) =>
        SendAsync<List<CategoryDto>>(HttpMethod.Get, $"api/events/{eventId}/categories");

    public Task<ApiResult<CategoryDto>> GetCategoryAsync(int id) =>
        SendAsync<CategoryDto>(HttpMethod.Get, $"api/categories/{id}");

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, SaveCategoryRequest request) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", Body(request));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, SaveCategoryRequest request) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}", Body(request));

    public Task<ApiResult> DeleteCategoryAsync(int id) =>
        SendPlainAsync(HttpMethod.Delete, $"api/categories/{id}");

    // =====================================================================
    // Enrolments
    // =====================================================================

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId, string? notes) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, "api/enrolments", Body(new { categoryId, notes }));

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/enrolments/me");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<EnrolmentDto>> CancelEnrolmentAsync(int id) =>
        SendAsync<EnrolmentDto>(HttpMethod.Put, $"api/enrolments/{id}/cancel");

    public Task<ApiResult<EnrolmentDto>> AssignBibAsync(int id, string bibNumber) =>
        SendAsync<EnrolmentDto>(HttpMethod.Put, $"api/enrolments/{id}/bib", Body(new { bibNumber }));

    // =====================================================================
    // Results
    // =====================================================================

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/results/me");

    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results");

    public Task<ApiResult<ResultDto>> CreateResultAsync(SaveResultRequest request) =>
        SendAsync<ResultDto>(HttpMethod.Post, "api/results", Body(request));

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int id, SaveResultRequest request) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{id}", Body(request));

    public Task<ApiResult> DeleteResultAsync(int id) =>
        SendPlainAsync(HttpMethod.Delete, $"api/results/{id}");

    // =====================================================================
    // Images
    // =====================================================================

    public async Task<ApiImage?> GetImageAsync(string path)
    {
        var encoded = string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        try
        {
            using var request = NewRequest(HttpMethod.Get, $"api/images/{encoded}");
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return new ApiImage
            {
                Bytes = await response.Content.ReadAsByteArrayAsync(),
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
            };
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            _logger.LogWarning(ex, "Could not load image {Path}", path);
            return null;
        }
    }

    // =====================================================================
    // Plumbing
    // =====================================================================

    private HttpRequestMessage NewRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var cookie = Session?.GetString(SessionKey);
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static JsonContent Body<T>(T value) => JsonContent.Create(value, options: Json);

    private static MultipartFormDataContent FileContent(IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var stream = new StreamContent(file.OpenReadStream());
        if (!MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType))
            mediaType = new MediaTypeHeaderValue("application/octet-stream");
        stream.Headers.ContentType = mediaType;
        form.Add(stream, "file", Path.GetFileName(file.FileName));
        return form;
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var request = NewRequest(method, url);
            request.Content = content;
            using var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode) return await FailureAsync<T>(response);

            T? data = default;
            if (response.StatusCode != HttpStatusCode.NoContent)
                data = await response.Content.ReadFromJsonAsync<T>(Json);

            return new ApiResult<T> { Success = true, StatusCode = (int)response.StatusCode, Data = data };
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            _logger.LogWarning(ex, "API call {Method} {Url} failed", method, url);
            return Unreachable<T>();
        }
    }

    private async Task<ApiResult> SendPlainAsync(HttpMethod method, string url)
    {
        try
        {
            using var request = NewRequest(method, url);
            using var response = await _http.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return new ApiResult { Success = true, StatusCode = (int)response.StatusCode };

            var failure = await FailureAsync<object>(response);
            return new ApiResult
            {
                Success = false, StatusCode = failure.StatusCode, Message = failure.Message,
                Errors = failure.Errors, Unreachable = failure.Unreachable
            };
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            _logger.LogWarning(ex, "API call {Method} {Url} failed", method, url);
            return new ApiResult { Success = false, StatusCode = 503, Unreachable = true, Message = UnreachableMessage };
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException;

    private static async Task<ApiResult<T>> FailureAsync<T>(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        string? message = null;
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(Json);
            message = body?.Message;
            if (body?.Errors is not null)
                foreach (var pair in body.Errors) errors[pair.Key] = pair.Value;
        }
        catch (Exception)
        {
            // The body was not our JSON error shape. We only ever show the friendly default below.
        }

        if (status == 500 || string.IsNullOrWhiteSpace(message))
        {
            message = status switch
            {
                401 => "Please log in to continue.",
                403 => "You don't have permission to do that.",
                404 => "We couldn't find what you were looking for.",
                >= 500 => "Something went wrong on our side. Please try again in a moment.",
                _ => GenericMessage
            };
        }

        return new ApiResult<T> { Success = false, StatusCode = status, Message = message!, Errors = errors };
    }

    private static ApiResult<T> Failed<T>(int status, string message) =>
        new() { Success = false, StatusCode = status, Message = message };

    private static ApiResult<T> Unreachable<T>() =>
        new() { Success = false, StatusCode = 503, Unreachable = true, Message = UnreachableMessage };

    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        foreach (var header in values)
        {
            if (header.StartsWith(ApiCookieName + "=", StringComparison.Ordinal))
                return header.Split(';')[0];   // "RaceDay.Session=abc123"
        }
        return null;
    }

    private class ApiErrorBody
    {
        public string? Message { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
