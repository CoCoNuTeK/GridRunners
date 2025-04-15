using GridRunners.Api.Configuration.Bind;

namespace GridRunners.Api.Configuration.Runtime;

/// <summary>
/// Runtime CORS configuration.
/// This class is used by services after application startup.
/// </summary>
public record RuntimeCorsConfig
{
    public string[]? AllowedOrigins { get; }
    public string[]? AllowedMethods { get; }
    public string[]? AllowedHeaders { get; }
    public bool AllowCredentials { get; }
    
    public RuntimeCorsConfig(CorsOptions options)
    {
        AllowedOrigins = options.AllowedOrigins;
        AllowedMethods = options.AllowedMethods ?? new[] { "GET", "POST", "PUT", "DELETE" };
        AllowedHeaders = options.AllowedHeaders ?? new[] { "Content-Type", "Authorization" };
        AllowCredentials = options.AllowCredentials;
    }
} 