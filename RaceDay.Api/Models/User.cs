namespace RaceDay.Api.Models;

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Hashed password. The plain password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Path of the profile picture stored in Azure Blob Storage (Part 3 addition).</summary>
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
