namespace GridRunners.Api.Configuration.Bind;

/// <summary>
/// Represents authentication settings loaded directly from appsettings.
/// This class is used only for initial configuration binding.
/// </summary>
public record AuthOptions
{
    public const string ConfigSection = "Auth";
    
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpirationHours { get; init; }
    public string SecretKeyName { get; init; } = string.Empty;
} 