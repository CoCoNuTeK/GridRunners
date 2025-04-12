using Microsoft.OpenApi.Models;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class SwaggerConfig
{
    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => 
        {
            // Configure Swagger doc for current version
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Grid Runners API",
                Version = ApiVersions.Current,
                Description = "A multiplayer maze game API where players use arrow keys to navigate. The first player to reach a specific square wins.",
                Contact = new OpenApiContact
                {
                    Name = "Grid Runners Team"
                }
            });

            // Add JWT Authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerWithUI(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        return app;
    }
} 