namespace GridRunners.Api.Configuration.Bind;

/// <summary>
/// Represents Azure Storage settings loaded from appsettings.
/// This class is used only for initial configuration binding.
/// </summary>
public record AzureStorageOptions
{
    public const string ConfigSection = "AzureStorage";
    
    public string AccountName { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
    public string UserImagesPath { get; init; } = string.Empty;
} 