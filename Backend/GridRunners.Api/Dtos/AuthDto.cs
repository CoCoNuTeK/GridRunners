using System.ComponentModel.DataAnnotations;

namespace GridRunners.Api.Dtos;

public record LoginRequest(
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    string Username,

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
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