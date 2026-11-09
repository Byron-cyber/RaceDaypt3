using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Common;

namespace RaceDay.Api.Controllers;

[Tags("Health")]
public class HealthController : ApiControllerBase
{
    /// <summary>Simple health check used by CI/CD and monitoring.</summary>
    /// <returns>200 with { "status": "Healthy" }.</returns>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { status = "Healthy" });
}
