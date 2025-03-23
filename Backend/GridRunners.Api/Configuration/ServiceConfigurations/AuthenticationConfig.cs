using System.Text;
using GridRunners.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class AuthenticationConfig
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Auth
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        var authOptions = authSection.Get<AuthOptions>()!;

        // Generate a secure key if none is provided
        if (string.IsNullOrEmpty(authOptions.Secret))
        {
            var generatedKey = AuthService.GenerateSecureKey();
            
            // Only in development, we generate and use a temporary key
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                authOptions = authOptions with { Secret = generatedKey };
                Console.WriteLine($"Generated new secure key: {generatedKey}");
                Console.WriteLine("Make sure to save this key in your appsettings.Development.json or secure storage for production.");
            }
            else
            {
                throw new InvalidOperationException("JWT Secret key is not configured in production!");
            }
        }

        // Register the configured instance
        services.AddSingleton(authOptions);
        services.AddScoped<AuthService>();

        // Configure JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = authOptions.Issuer,
                ValidAudience = authOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Secret))
            };

            // Configure for SignalR
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
} 