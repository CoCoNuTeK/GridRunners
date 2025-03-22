using System.Text;
using GridRunners.Api.Configuration;
using GridRunners.Api.Data;
using GridRunners.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure database
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("GridRunnersDb"));
}
else
{
    // For production, we'll use SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Configure Auth
var authSection = builder.Configuration.GetSection(AuthOptions.SectionName);
var authOptions = authSection.Get<AuthOptions>()!;

// Generate a secure key if none is provided
if (string.IsNullOrEmpty(authOptions.Secret))
{
    var generatedKey = AuthService.GenerateSecureKey();
    
    // Only in development, we generate and use a temporary key
    if (builder.Environment.IsDevelopment())
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
builder.Services.AddSingleton(authOptions);

builder.Services.AddScoped<AuthService>();

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

// Add SignalR
builder.Services.AddSignalR();

// Add rate limiting services
builder.Services.AddRateLimiter(RateLimitingService.ConfigureRateLimiting);

// Add Azure Storage configuration
var azureStorageSection = builder.Configuration.GetSection(AzureStorageOptions.SectionName);
builder.Services.Configure<AzureStorageOptions>(azureStorageSection);
var azureStorageOptions = azureStorageSection.Get<AzureStorageOptions>()!;
builder.Services.AddSingleton(azureStorageOptions);

// Add Azure Blob Storage service
builder.Services.AddSingleton<BlobStorageService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => 
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Grid Runners API",
        Version = "v1"
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add authentication and authorization to the pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

// Configure CORS for React frontend
app.UseCors(builder => builder
    .WithOrigins("http://localhost:3000") // Update with your React app's URL
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

app.Run();
