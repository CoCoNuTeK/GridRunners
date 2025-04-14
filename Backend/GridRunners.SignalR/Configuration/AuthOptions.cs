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

// Runtime configuration with secret loaded from Key Vault
public record RuntimeAuthConfig
{
    public string Issuer { get; }
    public string Audience { get; }
    public int ExpirationHours { get; }
    public string Secret { get; }
    
    public RuntimeAuthConfig(AuthOptions options, string secret)
    {
        Issuer = options.Issuer;
        Audience = options.Audience;
        ExpirationHours = options.ExpirationHours;
        Secret = secret;
    }
} 