namespace GridRunners.Api.Configuration;

public class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";
    
    public string AccountName { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
    public string UserImagesPath { get; set; } = null!;
} 