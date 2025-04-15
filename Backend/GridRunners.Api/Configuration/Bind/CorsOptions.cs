using System;

namespace GridRunners.Api.Configuration.Bind;

/// <summary>
/// Represents CORS settings loaded from appsettings.
/// This class is used only for initial configuration binding.
/// </summary>
public record CorsOptions
{
    public const string ConfigSection = "Cors";
    
    public string[]? AllowedOrigins { get; init; }
    public string[]? AllowedMethods { get; init; }
    public string[]? AllowedHeaders { get; init; }
    public bool AllowCredentials { get; init; } = false;
} 