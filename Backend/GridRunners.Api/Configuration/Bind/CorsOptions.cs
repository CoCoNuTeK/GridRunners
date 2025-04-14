using System;

namespace GridRunners.Api.Configuration.Bind;

/// <summary>
/// Represents CORS settings loaded from appsettings.
/// This class is used only for initial configuration binding.
/// </summary>
public record CorsOptions
{
    public const string ConfigSection = "Cors";
    
    public string[] AllowedOrigins { get; init; } = Array.Empty<string>();
    public bool AllowCredentials { get; init; } = true;
    public string[] AllowedMethods { get; init; } = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" };
    public string[] AllowedHeaders { get; init; } = new[] { "*" };
} 