using System.Text;
using GridRunners.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Azure.Security.KeyVault.Secrets;
using GridRunners.Api.Configuration.Bind;
using GridRunners.Api.Configuration.Runtime;

namespace GridRunners.Api.Configuration.ServiceConfigurations;

public static class AuthenticationConfig
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind auth options directly from configuration
        var authOptions = new AuthOptions();
        configuration.GetSection(AuthOptions.ConfigSection).Bind(authOptions);
        
        // Register SecretClient accessor
        services.AddTransient<Func<SecretClient>>(provider => () => provider.GetRequiredService<SecretClient>());
        
        // Register RuntimeAuthConfig as singleton with factory pattern
        services.AddSingleton(provider => CreateRuntimeAuthConfig(provider, authOptions));
        
        // Register AuthService as scoped
        services.AddScoped<AuthService>();
        
        // Configure JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();
        
        // Configure JWT Bearer with post-configuration pattern
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<RuntimeAuthConfig>((options, authConfig) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authConfig.Issuer,
                    ValidAudience = authConfig.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authConfig.Secret))
                };
                
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context => Task.CompletedTask
                };
            });

        return services;
    }

    // Factory method to create the RuntimeAuthConfig with proper error handling
    private static RuntimeAuthConfig CreateRuntimeAuthConfig(IServiceProvider provider, AuthOptions authOptions)
    {
        var secretClientFactory = provider.GetRequiredService<Func<SecretClient>>();
        var secretClient = secretClientFactory();
        
        try
        {
            // Get secret from Key Vault
            var secretName = string.IsNullOrEmpty(authOptions.SecretKeyName) 
                ? "jwt-secret-key" 
                : authOptions.SecretKeyName;
            
            var jwtSecret = secretClient.GetSecret(secretName).Value.Value;
            Console.WriteLine($"Retrieved JWT secret from Key Vault with key: {secretName}");
            
            // Create runtime config with secret
            return new RuntimeAuthConfig(authOptions, jwtSecret);
        }
        catch (Exception ex)
        {
            // Log the error and fail
            Console.WriteLine($"Error retrieving JWT secret from Key Vault: {ex.Message}");
            throw new InvalidOperationException($"Failed to retrieve JWT Secret key from Key Vault: {ex.Message}");
        }
    }
} 