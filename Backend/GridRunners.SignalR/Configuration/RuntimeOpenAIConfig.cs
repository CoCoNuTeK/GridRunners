namespace GridRunners.SignalR.Configuration;

/// <summary>
/// Runtime OpenAI configuration with secrets loaded from Key Vault.
/// This class is used by services after application startup.
/// </summary>
public record RuntimeOpenAIConfig
{
    public bool Enabled { get; }
    public string ModelId { get; }
    public string ApiKey { get; }
    public string OrganizationId { get; }
    public string ProjectId { get; }
    public string EndpointUrl { get; }
    
    public RuntimeOpenAIConfig(
        OpenAIOptions options, 
        string modelId, 
        string apiKey, 
        string organizationId, 
        string projectId,
        string endpointUrl)
    {
        Enabled = options.Enabled;
        ModelId = modelId;
        ApiKey = apiKey;
        OrganizationId = organizationId;
        ProjectId = projectId;
        EndpointUrl = endpointUrl;
    }
} 