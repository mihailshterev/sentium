namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Per named HttpClient: which server it targets and the network timeout the OpenAI pipeline applies.
/// </summary>
internal sealed class InferenceHttpClientOptions
{
    public InferenceEndpoint? Endpoint { get; set; }

    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromMinutes(30);
}
