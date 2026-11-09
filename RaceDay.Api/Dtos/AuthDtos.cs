using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Details needed to create an account.</summary>
public class RegisterRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored hashed, never in plain text.</summary>
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;

    /// <summary>Must be exactly "Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;

    [StringLength(20)] public string? PhoneNumber { get; set; }

    /// <summary>Format YYYY-MM-DD.</summary>
    public DateOnly? DateOfBirth { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    /// <summary>Lifetime of the session cookie in seconds.</summary>
    public int ExpiresIn { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Path of the profile picture served by this API (GET /api/images/...). Null if none.</summary>
    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Limited user information shown to organisers.</summary>
public class UserSummaryDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [StringLength(20)] public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
