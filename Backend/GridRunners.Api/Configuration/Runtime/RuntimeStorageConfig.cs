using GridRunners.Api.Configuration.Bind;

namespace GridRunners.Api.Configuration.Runtime;

/// <summary>
/// Runtime Azure Storage configuration.
/// This class is used by services after application startup.
/// </summary>
public record RuntimeStorageConfig
{
    public string AccountName { get; }
    public string ContainerName { get; }
    public string UserImagesPath { get; }
    public string BlobUri { get; }
    
    public RuntimeStorageConfig(AzureStorageOptions options)
    {
        AccountName = options.AccountName;
        ContainerName = options.ContainerName;
        UserImagesPath = options.UserImagesPath;
        BlobUri = $"https://{options.AccountName}.blob.core.windows.net";
    }
} 