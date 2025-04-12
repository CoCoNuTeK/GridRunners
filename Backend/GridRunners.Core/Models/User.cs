using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace GridRunners.Core.Models;

public class User
{
    [Key]
    public int Id { get; init; }
    
    [Required]
    [StringLength(30, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", 
        ErrorMessage = "Username can only contain letters, numbers, underscores, and hyphens")]
    public string Username { get; init; } = null!;
    
    [Required]
    public string PasswordHash { get; private set; } = null!;
    
    [StringLength(50)]
    public string? DisplayName { get; private set; }
    
    public string? ProfileImageUrl { get; private set; }
    
    // Refresh token properties
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }
    
    // Navigation property for N:N relationship with MazeGame
    public virtual ICollection<MazeGame> Games { get; init; } = new List<MazeGame>();

    // Default constructor for EF Core
    protected User() { }

    // Factory method for creating new users
    public static User CreateNew(string username, string password)
    {
        // Validate username
        if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 30)
        {
            throw new ArgumentException("Username must be between 3 and 30 characters long", nameof(username));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$"))
        {
            throw new ArgumentException("Username can only contain letters, numbers, underscores, and hyphens", nameof(username));
        }

        // Validate password
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password is required", nameof(password));
        }

        if (password.Length < 6 || password.Length > 64)
        {
            throw new ArgumentException("Password must be between 6 and 64 characters long", nameof(password));
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            throw new ArgumentException("Password must contain at least one uppercase letter, one lowercase letter, and one number", nameof(password));
        }

        return new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            DisplayName = username // Default display name is username
        };
    }

    // Verify password
    public bool VerifyPassword(string password)
    {
        return PasswordHash == HashPassword(password);
    }

    // Public methods to modify properties
    public void UpdateDisplayName(string? displayName) => DisplayName = displayName;
    
    public void UpdateProfileImage(string? imageUrl)
    {
        ProfileImageUrl = imageUrl;
    }
    
    public void UpdatePassword(string newPassword) => PasswordHash = HashPassword(newPassword);

    // Refresh token methods
    public void SetRefreshToken(string token, DateTime expiresAt)
    {
        RefreshToken = token;
        RefreshTokenExpiresAt = expiresAt;
    }

    public bool IsRefreshTokenValid(string token)
    {
        return RefreshToken == token && 
               RefreshTokenExpiresAt.HasValue && 
               RefreshTokenExpiresAt.Value > DateTime.UtcNow;
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAt = null;
    }

    // Private helper for password hashing
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}