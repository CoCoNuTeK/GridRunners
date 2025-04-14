using GridRunners.Api.Configuration.Bind;

namespace GridRunners.Api.Configuration.Runtime;

/// <summary>
/// Runtime authentication configuration with secret loaded from Key Vault.
/// This class is used by services after application startup.
/// </summary>
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