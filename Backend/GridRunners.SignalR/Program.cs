using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GridRunners.Core.Data;
using GridRunners.SignalR.Configuration;
using GridRunners.SignalR.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSignalR();
builder.Services.AddCors();

// Configure database - use SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Key Vault
var keyVaultOptions = builder.Configuration.GetSection(KeyVaultOptions.SectionName).Get<KeyVaultOptions>()
    ?? throw new InvalidOperationException("Key Vault URL is not configured. Check your appsettings files.");

// Create DefaultAzureCredential
var credential = new DefaultAzureCredential();
var secretClient = new SecretClient(new Uri(keyVaultOptions.Url), credential);
builder.Services.AddSingleton(secretClient);
Console.WriteLine($"Added SecretClient for KeyVault at {keyVaultOptions.Url}");

// Configure Authentication
var authSection = builder.Configuration.GetSection(AuthOptions.SectionName);
var authOptions = authSection.Get<AuthOptions>()!;

// Get JWT secret from KeyVault
string jwtSecret;
try
{
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
    Console.WriteLine($"Error retrieving JWT secret from Key Vault: {ex.Message}");
    throw new InvalidOperationException($"Failed to retrieve JWT Secret key from Key Vault: {ex.Message}");
}

// Set the secret in the auth options
authOptions = authOptions with { Secret = jwtSecret };
builder.Services.AddSingleton(authOptions);

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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Configure CORS using settings from configuration
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("CORS configuration is missing. Please ensure 'Cors:AllowedOrigins' is configured.");

app.UseCors(builder => builder
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

// Map hub
app.MapHub<MazeGameHub>("/hubs/game");

app.Run();
