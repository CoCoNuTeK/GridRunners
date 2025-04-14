namespace GridRunners.SignalR.Configuration;

public record CorsOptions
{
    public const string SectionName = "Cors";
    
    public string[] AllowedOrigins { get; init; } = Array.Empty<string>();
    public bool AllowCredentials { get; init; } = true;
    public string[] AllowedMethods { get; init; } = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" };
    public string[] AllowedHeaders { get; init; } = new[] { "*" };
} 