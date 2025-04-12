using System.Text;
using GridRunners.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Azure.Security.KeyVault.Secrets;
using GridRunners.Api.Configuration;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class AuthenticationConfig
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Auth
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        var authOptions = authSection.Get<AuthOptions>()!;
        
        // Get JWT secret from KeyVault
        var provider = services.BuildServiceProvider();
        var secretClient = provider.GetRequiredService<SecretClient>();
        
        string jwtSecret;
        try
        {
            // Try to get secret from Key Vault
            var secretName = authOptions.SecretKeyName;
            if (string.IsNullOrEmpty(secretName))
            {
                secretName = "jwt-secret-key";
            }
            
            jwtSecret = secretClient.GetSecret(secretName).Value.Value;
            Console.WriteLine($"Retrieved JWT secret from Key Vault with key: {secretName}");
        }
        catch (Exception ex)
        {
            // Log the error and fail
            Console.WriteLine($"Error retrieving JWT secret from Key Vault: {ex.Message}");
            throw new InvalidOperationException($"Failed to retrieve JWT Secret key from Key Vault: {ex.Message}");
        }
        
        // Set the secret in the auth options
        authOptions = authOptions with { Secret = jwtSecret };

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