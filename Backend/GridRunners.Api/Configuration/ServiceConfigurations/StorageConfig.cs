using GridRunners.Api.Data;
using GridRunners.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class StorageConfig
{
    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Configure database
        if (environment.IsDevelopment())
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("GridRunnersDb"));
        }
        else
        {
            // For production, we'll use SQL Server
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        }

        // Add Azure Storage configuration
        var azureStorageSection = configuration.GetSection(AzureStorageOptions.SectionName);
        services.Configure<AzureStorageOptions>(azureStorageSection);
        var azureStorageOptions = azureStorageSection.Get<AzureStorageOptions>()!;
        services.AddSingleton(azureStorageOptions);

        // Add Azure Blob Storage service
        services.AddSingleton<BlobStorageService>();

        return services;
    }
} 