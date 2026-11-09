using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Common;

[ApiController]
[Route("api")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user (read from the session cookie claims).</summary>
    protected int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    protected bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    protected bool IsOrganiser => User.IsInRole(RoleNames.Organiser);
    protected bool IsParticipant => User.IsInRole(RoleNames.Participant);

    protected ObjectResult Error(int status, string message, IDictionary<string, string[]>? errors = null)
        => StatusCode(status, new ApiError { Message = message, Errors = errors });

    protected ObjectResult BadRequestError(string message, string? field = null)
        => Error(StatusCodes.Status400BadRequest, message,
            field is null ? null : new Dictionary<string, string[]> { [field] = new[] { message } });

    protected ObjectResult NotFoundError(string message) => Error(StatusCodes.Status404NotFound, message);
    protected ObjectResult ForbiddenError(string message) => Error(StatusCodes.Status403Forbidden, message);
    protected ObjectResult ConflictError(string message) => Error(StatusCodes.Status409Conflict, message);
}
