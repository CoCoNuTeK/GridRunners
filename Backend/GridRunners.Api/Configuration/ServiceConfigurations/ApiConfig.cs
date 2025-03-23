using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GridRunners.Api.Services;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class ApiConfig
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddSignalR();

        // Configure API Versioning
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            
            // Support both URL segment and query string versioning
            options.ApiVersionReader = Microsoft.AspNetCore.Mvc.Versioning.ApiVersionReader.Combine(
                new Microsoft.AspNetCore.Mvc.Versioning.UrlSegmentApiVersionReader(),
                new Microsoft.AspNetCore.Mvc.Versioning.QueryStringApiVersionReader("api-version")
            );
        });

        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
        });

        // Add rate limiting services
        services.AddRateLimiter(RateLimitingService.ConfigureRateLimiting);

        return services;
    }

    public static IApplicationBuilder UseApiConfiguration(this IApplicationBuilder app, IConfiguration configuration)
    {
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        // Configure CORS using settings from configuration
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins?.Length > 0)
        {
            app.UseCors(builder => builder
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
        }
        else
        {
            throw new InvalidOperationException(
                "CORS configuration is missing. Please ensure 'Cors:AllowedOrigins' is configured " +
                "either in appsettings.Development.json for local development " +
                "or as environment variables (Cors__AllowedOrigins__0, Cors__AllowedOrigins__1, etc.) for production."
            );
        }

        return app;
    }
} 