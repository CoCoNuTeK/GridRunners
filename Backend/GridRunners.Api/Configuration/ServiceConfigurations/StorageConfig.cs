using GridRunners.Core.Data;
using GridRunners.Api.Services;
using Microsoft.EntityFrameworkCore;
using GridRunners.Api.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class StorageConfig
{
    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Configure database - use SQL Server for both dev and prod
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Configure Azure Storage 
        var azureStorageSection = configuration.GetSection(AzureStorageOptions.SectionName);
        services.Configure<AzureStorageOptions>(azureStorageSection);
        var azureStorageOptions = azureStorageSection.Get<AzureStorageOptions>()!;
        services.AddSingleton(azureStorageOptions);

        // Create DefaultAzureCredential (used for both Key Vault and Blob Storage)
        var credential = new DefaultAzureCredential();
        
        // Create and register BlobServiceClient
        var blobServiceClient = new BlobServiceClient(
            new Uri($"https://{azureStorageOptions.AccountName}.blob.core.windows.net"),
            credential);
        services.AddSingleton(blobServiceClient);
        Console.WriteLine($"Added BlobServiceClient for storage account {azureStorageOptions.AccountName}");

        // Add Azure Blob Storage service
        services.AddSingleton<BlobStorageService>();
        
        // Configure Key Vault
        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));
        var keyVaultOptions = configuration.GetSection(KeyVaultOptions.SectionName).Get<KeyVaultOptions>();
        
        if (keyVaultOptions != null && !string.IsNullOrEmpty(keyVaultOptions.Url))
        {
            // Use the same credential instance for Key Vault
            var secretClient = new SecretClient(new Uri(keyVaultOptions.Url), credential);
            services.AddSingleton(secretClient);
            Console.WriteLine($"Added SecretClient for KeyVault at {keyVaultOptions.Url}");
        }
        else
        {
            throw new InvalidOperationException("Key Vault URL is not configured. Check your appsettings files.");
        }

        return services;
    }
} 