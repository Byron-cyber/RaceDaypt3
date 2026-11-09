using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your first name."), StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your last name."), StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address."), StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please choose a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Please choose how you will use RaceDay.")]
    [Display(Name = "I am a")]
    public string Role { get; set; } = "Participant";

    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your first name."), StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your last name."), StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    // Read-only details shown on the page
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
