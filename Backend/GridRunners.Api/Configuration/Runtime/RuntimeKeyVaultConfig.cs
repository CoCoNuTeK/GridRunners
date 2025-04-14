using GridRunners.Api.Configuration.Bind;

namespace GridRunners.Api.Configuration.Runtime;

/// <summary>
/// Runtime Key Vault configuration.
/// This class is used by services after application startup.
/// </summary>
public record RuntimeKeyVaultConfig
{
    public string Url { get; }
    
    public RuntimeKeyVaultConfig(KeyVaultOptions options)
    {
        Url = options.Url;
    }
} 