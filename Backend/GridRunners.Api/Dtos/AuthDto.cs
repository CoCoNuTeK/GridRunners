using System.ComponentModel.DataAnnotations;

namespace GridRunners.Api.Dtos;

public record LoginRequest(
    [Required(ErrorMessage = "Username is required")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 30 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and hyphens")]
    string Username,

    [Required(ErrorMessage = "Password is required")]
    [StringLength(64, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 64 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d]{6,}$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number")]
    string Password
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    string Username
);

public record RefreshTokenRequest(
    [Required]
    string RefreshToken
); 