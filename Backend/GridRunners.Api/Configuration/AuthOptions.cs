namespace GridRunners.Api.Configuration;

public record AuthOptions
{
    public const string SectionName = "Auth";
    
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public string Secret { get; init; } = null!;
    public int ExpirationHours { get; init; }
    public string SecretKeyName { get; init; } = null!;
} 