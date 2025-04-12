namespace GridRunners.SignalR.Configuration;

public record KeyVaultOptions
{
    public const string SectionName = "KeyVault";
    public string Url { get; set; } = string.Empty;
} 