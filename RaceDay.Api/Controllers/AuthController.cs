using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Register, log in and log out. Login starts a cookie-based session that stores the user's id and role.</summary>
[Route("api/auth")]
[Tags("Authentication")]
public class AuthController : ApiControllerBase
{
    private const int SessionHours = 2;

    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(ApplicationDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Create a new account as either an Organiser or a Participant.</summary>
    /// <remarks>
    /// Expects first name, last name, email, password (min 8 characters), role ("Organiser" or "Participant"),
    /// and optionally phone number and date of birth (YYYY-MM-DD). The password is hashed before it is stored.
    /// </remarks>
    /// <returns>201 with the new user (never the password), 400 for invalid data, 409 if the email is already registered.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Role is a validated choice - it must be one of the two known roles, never a free-text value or id.
        var roleName = RoleNames.All.FirstOrDefault(r =>
            string.Equals(r, request.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (roleName is null)
            return BadRequestError("Role must be either 'Organiser' or 'Participant'.", "role");

        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequestError("Date of birth cannot be in the future.", "dateOfBirth");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return ConflictError("An account with this email already exists.");

        var role = await _db.Roles.SingleAsync(r => r.RoleName == roleName);

        var user = new User
        {
            RoleId = role.RoleId,
            Role = role,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            DateOfBirth = request.DateOfBirth
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two people registering the same email at the same moment hit the unique index.
            return ConflictError("An account with this email already exists.");
        }

        return CreatedAtAction(nameof(UsersController.GetMyProfile), "Users", null, user.ToDto());
    }

    /// <summary>Log in with email and password.</summary>
    /// <remarks>
    /// On success a session cookie is issued. The browser (and Swagger UI) sends it automatically on later
    /// requests, which keeps the user's identity and role for the whole session.
    /// </remarks>
    /// <returns>200 with the session lifetime and user details, 401 for wrong email or password.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        // Same message for "no such user" and "wrong password" so attackers can't discover which emails exist.
        const string invalid = "Invalid email or password.";
        if (user is null || !user.IsActive)
            return Error(StatusCodes.Status401Unauthorized, invalid);

        var check = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
            return Error(StatusCodes.Status401Unauthorized, invalid);

        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role!.RoleName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return Ok(new LoginResponse { ExpiresIn = SessionHours * 3600, User = user.ToDto() });
    }

    /// <summary>End the current session.</summary>
    /// <returns>204 when logged out, 401 if there was no session.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
