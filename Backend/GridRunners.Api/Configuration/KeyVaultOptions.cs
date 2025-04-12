namespace GridRunners.Api.Configuration;

public class KeyVaultOptions
{
    public const string SectionName = "KeyVault";
    
    public string Url { get; set; } = null!;
} 