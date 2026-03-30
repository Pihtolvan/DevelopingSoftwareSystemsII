using System.ComponentModel.DataAnnotations;

namespace Todo.Api.Dtos.Auth;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
}
