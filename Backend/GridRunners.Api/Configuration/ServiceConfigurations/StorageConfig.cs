using GridRunners.Core.Data;
using GridRunners.Api.Services;
using Microsoft.EntityFrameworkCore;
using GridRunners.Api.Configuration.Bind;
using GridRunners.Api.Configuration.Runtime;
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

        // Bind configuration sections directly to options objects
        var azureStorageOptions = new AzureStorageOptions();
        configuration.GetSection(AzureStorageOptions.ConfigSection).Bind(azureStorageOptions);
        
        var keyVaultOptions = new KeyVaultOptions();
        configuration.GetSection(KeyVaultOptions.ConfigSection).Bind(keyVaultOptions);
        
        // Create runtime configurations
        var runtimeStorageConfig = new RuntimeStorageConfig(azureStorageOptions);
        var runtimeKeyVaultConfig = new RuntimeKeyVaultConfig(keyVaultOptions);
        
        // Register runtime configurations with DI
        services.AddSingleton(runtimeStorageConfig);
        services.AddSingleton(runtimeKeyVaultConfig);

        // Create DefaultAzureCredential (used for both Key Vault and Blob Storage)
        var credential = new DefaultAzureCredential();
        
        // Create and register BlobServiceClient
        var blobServiceClient = new BlobServiceClient(
            new Uri(runtimeStorageConfig.BlobUri),
            credential);
        services.AddSingleton(blobServiceClient);
        Console.WriteLine($"Added BlobServiceClient for storage account {runtimeStorageConfig.AccountName}");

        // Add Azure Blob Storage service
        services.AddSingleton<BlobStorageService>();
        
        // Configure Key Vault client
        if (!string.IsNullOrEmpty(runtimeKeyVaultConfig.Url))
        {
            // Use the same credential instance for Key Vault
            var secretClient = new SecretClient(new Uri(runtimeKeyVaultConfig.Url), credential);
            services.AddSingleton(secretClient);
            Console.WriteLine($"Added SecretClient for KeyVault at {runtimeKeyVaultConfig.Url}");
        }
        else
        {
            throw new InvalidOperationException("Key Vault URL is not configured. Check your appsettings files.");
        }

        return services;
    }
} 