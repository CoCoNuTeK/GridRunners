namespace GridRunners.SignalR.Configuration;

/// <summary>
/// Represents OpenAI API settings loaded from appsettings.
/// This class is used only for initial configuration binding.
/// Contains key names for values stored in KeyVault.
/// </summary>
public record OpenAIOptions
{
    public const string ConfigSection = "OpenAI";
    
    public bool Enabled { get; init; } = false;
    public string ModelIdKeyName { get; init; } = string.Empty;
    public string ApiKeyName { get; init; } = string.Empty;
    public string OrganizationIdKeyName { get; init; } = string.Empty;
    public string ProjectIdKeyName { get; init; } = string.Empty;
    public string EndpointUrlKeyName { get; init; } = string.Empty;
} 