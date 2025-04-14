using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GridRunners.Api.Services;
using GridRunners.Api.Configuration.Runtime;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class ApiConfig
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddCors();

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

        // Get the CORS options from DI
        var corsConfig = app.ApplicationServices.GetRequiredService<RuntimeCorsConfig>();
        
        if (corsConfig.AllowedOrigins?.Length > 0)
        {
            app.UseCors(builder => 
            {
                var corsBuilder = builder
                    .WithOrigins(corsConfig.AllowedOrigins)
                    .WithMethods(corsConfig.AllowedMethods)
                    .WithHeaders(corsConfig.AllowedHeaders);
                
                if (corsConfig.AllowCredentials)
                {
                    corsBuilder.AllowCredentials();
                }
                else
                {
                    corsBuilder.DisallowCredentials();
                }
            });
        }
        else
        {
            throw new InvalidOperationException(
                "CORS configuration is missing. Please ensure 'Cors:AllowedOrigins' is configured " +
                "in appsettings.json or as environment variables."
            );
        }

        return app;
    }
} 