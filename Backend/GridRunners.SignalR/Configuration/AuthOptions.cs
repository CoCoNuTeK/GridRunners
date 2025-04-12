namespace GridRunners.SignalR.Configuration;

public record AuthOptions
{
    public const string SectionName = "Authentication";
    
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public string SecretKeyName { get; init; } = string.Empty;
    public int ExpirationHours { get; init; } = 24;
} 