using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GridRunners.Core.Data;
using GridRunners.SignalR.Configuration;
using GridRunners.SignalR.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using GridRunners.SignalR.Services;
using GridRunners.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSignalR();
builder.Services.AddCors();

// Configure database - use SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Bind configuration sections directly to options objects
var keyVaultOptions = new KeyVaultOptions();
builder.Configuration.GetSection(KeyVaultOptions.SectionName).Bind(keyVaultOptions);

if (string.IsNullOrEmpty(keyVaultOptions.Url))
{
    throw new InvalidOperationException("Key Vault URL is not configured. Check your appsettings files.");
}

// Create DefaultAzureCredential
var credential = new DefaultAzureCredential();
var secretClient = new SecretClient(new Uri(keyVaultOptions.Url), credential);
builder.Services.AddSingleton(secretClient);
Console.WriteLine($"Added SecretClient for KeyVault at {keyVaultOptions.Url}");

// Bind Authentication options
var authOptions = new AuthOptions();
builder.Configuration.GetSection(AuthOptions.SectionName).Bind(authOptions);

// Get JWT secret from KeyVault
string jwtSecret;
try
{
    var secretName = string.IsNullOrEmpty(authOptions.SecretKeyName) 
        ? "jwt-secret-key" 
        : authOptions.SecretKeyName;
    
    jwtSecret = secretClient.GetSecret(secretName).Value.Value;
    Console.WriteLine($"Retrieved JWT secret from Key Vault with key: {secretName}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error retrieving JWT secret from Key Vault: {ex.Message}");
    throw new InvalidOperationException($"Failed to retrieve JWT Secret key from Key Vault: {ex.Message}");
}

// Create runtime auth configuration with the secret
var runtimeAuthConfig = new RuntimeAuthConfig(authOptions, jwtSecret);
builder.Services.AddSingleton(runtimeAuthConfig);

// Configure OpenAI services
var openAIOptions = new OpenAIOptions();
builder.Configuration.GetSection(OpenAIOptions.ConfigSection).Bind(openAIOptions);

if (openAIOptions.Enabled)
{
    try
    {
        // Get secrets from Key Vault
        var modelId = secretClient.GetSecret(openAIOptions.ModelIdKeyName).Value.Value;
        var apiKey = secretClient.GetSecret(openAIOptions.ApiKeyName).Value.Value;
        var organizationId = secretClient.GetSecret(openAIOptions.OrganizationIdKeyName).Value.Value;
        var projectId = secretClient.GetSecret(openAIOptions.ProjectIdKeyName).Value.Value;
        var endpointUrl = secretClient.GetSecret(openAIOptions.EndpointUrlKeyName).Value.Value;
        
        // Create runtime config
        var runtimeOpenAIConfig = new RuntimeOpenAIConfig(
            openAIOptions,
            modelId,
            apiKey,
            organizationId,
            projectId,
            endpointUrl
        );
        
        // Register with DI
        builder.Services.AddSingleton(runtimeOpenAIConfig);
        Console.WriteLine("Successfully loaded OpenAI configuration from KeyVault");
        
        // Register HttpClient and OpenAI service for maze generation
        builder.Services.AddHttpClient<OpenAIService>();
        builder.Services.AddScoped<OpenAIService>();
        builder.Services.AddScoped<IMazeGenerationService, OpenAIService>();
    }
    catch (Exception ex)
    {
        // Log the error but don't fail startup - service will be disabled
        Console.WriteLine($"Error loading OpenAI configuration from KeyVault: {ex.Message}");
        
        // Register a disabled configuration
        var disabledConfig = new RuntimeOpenAIConfig(
            new OpenAIOptions { Enabled = false },
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        );
        builder.Services.AddSingleton(disabledConfig);
    }
}
else
{
    // Register a disabled configuration
    var disabledConfig = new RuntimeOpenAIConfig(
        openAIOptions,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty
    );
    builder.Services.AddSingleton(disabledConfig);
    Console.WriteLine("OpenAI integration is disabled by configuration");
}

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
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
        ValidIssuer = runtimeAuthConfig.Issuer,
        ValidAudience = runtimeAuthConfig.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(runtimeAuthConfig.Secret))
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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Bind CORS options directly
var corsOptions = new CorsOptions();
builder.Configuration.GetSection(CorsOptions.SectionName).Bind(corsOptions);

if (corsOptions.AllowedOrigins?.Length > 0)
{
    app.UseCors(builder => builder
        .WithOrigins(corsOptions.AllowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
}
else
{
    throw new InvalidOperationException("CORS configuration is missing. Please ensure 'Cors:AllowedOrigins' is configured.");
}

// Map hub
app.MapHub<MazeGameHub>("/hubs/game");

app.Run();
