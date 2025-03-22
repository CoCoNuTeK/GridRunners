using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace GridRunners.Api.Models;

public class User
{
    [Key]
    public int Id { get; init; }
    
    [Required]
    [StringLength(50)]
    public string Username { get; init; } = null!;
    
    [Required]
    public string PasswordHash { get; private set; } = null!;
    
    [StringLength(50)]
    public string? DisplayName { get; private set; }
    
    public string? ProfileImageUrl { get; private set; }
    
    // New properties for SAS token management
    public string? ProfileImageSasToken { get; private set; }
    public DateTime? ProfileImageSasExpiration { get; private set; }
    
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
    
    public void UpdateProfileImage(string? imageUrl, string? sasToken = null, DateTime? sasExpiration = null)
    {
        ProfileImageUrl = imageUrl;
        ProfileImageSasToken = sasToken;
        ProfileImageSasExpiration = sasExpiration;
    }

    public bool NeedsProfileImageSasRefresh()
    {
        if (string.IsNullOrEmpty(ProfileImageUrl)) return false;
        if (ProfileImageSasExpiration == null) return true;
        
        // Return true if token expires in less than 1 hour or has expired
        return ProfileImageSasExpiration.Value.AddHours(-1) <= DateTime.UtcNow;
    }

    public string? GetProfileImageUrlWithSas()
    {
        if (string.IsNullOrEmpty(ProfileImageUrl)) return null;
        if (string.IsNullOrEmpty(ProfileImageSasToken)) return ProfileImageUrl;
        return $"{ProfileImageUrl}?{ProfileImageSasToken}";
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