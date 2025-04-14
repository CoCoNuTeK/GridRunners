namespace GridRunners.Api.Configuration.Bind;

/// <summary>
/// Represents Azure Key Vault settings loaded from appsettings.
/// This class is used only for initial configuration binding.
/// </summary>
public record KeyVaultOptions
{
    public const string ConfigSection = "KeyVault";
    
    public string Url { get; init; } = string.Empty;
} 