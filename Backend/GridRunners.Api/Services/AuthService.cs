using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GridRunners.Api.Configuration.Runtime;
using GridRunners.Core.Models;
using Microsoft.IdentityModel.Tokens;

namespace GridRunners.Api.Services;

public class AuthService
{
    private readonly RuntimeAuthConfig _authConfig;

    public AuthService(RuntimeAuthConfig authConfig)
    {
        _authConfig = authConfig;
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),    // User ID
            new Claim(ClaimTypes.Name, user.Username), // Username
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Token ID
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) // Issued at
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authConfig.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(30); // Short-lived access token

        var token = new JwtSecurityToken(
            issuer: _authConfig.Issuer,
            audience: _authConfig.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        return DateTime.UtcNow.AddHours(_authConfig.ExpirationHours);
    }

    public static string GenerateSecureKey()
    {
        var key = new byte[32]; // 256 bits
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return Convert.ToBase64String(key);
    }
} 