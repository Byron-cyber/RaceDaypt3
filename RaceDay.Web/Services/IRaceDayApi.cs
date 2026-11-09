using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>Everything the MVC app can ask the RaceDay API to do. There is no other data access in this project.</summary>
public interface IRaceDayApi
{
    // Authentication
    Task<ApiResult<LoginResponse>> LoginAsync(string email, string password);
    Task<ApiResult<UserDto>> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();

    // Profile
    Task<ApiResult<UserDto>> GetMyProfileAsync();
    Task<ApiResult<UserDto>> UpdateMyProfileAsync(UpdateProfileRequest request);
    Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file);

    // Events
    Task<ApiResult<PagedList<EventDto>>> GetEventsAsync(EventFilter filter);
    Task<ApiResult<EventDetailDto>> GetEventAsync(int id);
    Task<ApiResult<List<EventDto>>> GetMyEventsAsync();
    Task<ApiResult<EventDto>> CreateEventAsync(SaveEventRequest request);
    Task<ApiResult<EventDto>> UpdateEventAsync(int id, SaveEventRequest request);
    Task<ApiResult> CancelEventAsync(int id);
    Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file);

    // Categories
    Task<ApiResult<List<CategoryDto>>> GetCategoriesAsync(int eventId);
    Task<ApiResult<CategoryDto>> GetCategoryAsync(int id);
    Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, SaveCategoryRequest request);
    Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, SaveCategoryRequest request);
    Task<ApiResult> DeleteCategoryAsync(int id);

    // Enrolments
    Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId, string? notes);
    Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync();
    Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId);
    Task<ApiResult<EnrolmentDto>> CancelEnrolmentAsync(int id);
    Task<ApiResult<EnrolmentDto>> AssignBibAsync(int id, string bibNumber);

    // Results
    Task<ApiResult<List<ResultDto>>> GetMyResultsAsync();
    Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId);
    Task<ApiResult<ResultDto>> CreateResultAsync(SaveResultRequest request);
    Task<ApiResult<ResultDto>> UpdateResultAsync(int id, SaveResultRequest request);
    Task<ApiResult> DeleteResultAsync(int id);

    // Images (served by the API from Azure Blob Storage)
    Task<ApiImage?> GetImageAsync(string path);
}
