using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Builds Microsoft.Extensions.AI clients over the OpenAI-compatible API of the inference servers, on
/// top of a named HttpClient registered with
/// <see cref="InferenceServiceCollectionExtensions.AddInferenceHttpClient"/>.
/// <para>
/// The model passed here is the client's default; <c>ChatOptions.ModelId</c> still overrides it per request
/// (the OpenAI adapter sends it as the request's <c>model</c>). Clients are cheap to create; callers that
/// reuse one for a while (an agent run, a singleton pipeline) keep it themselves.
/// </para>
/// </summary>
public interface IInferenceClientFactory
{
    IChatClient CreateChatClient(string httpClientName, string model);

    IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(string httpClientName, string model);
}

internal sealed class InferenceClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<InferenceOptions> options,
    IOptionsMonitor<InferenceHttpClientOptions> httpClientOptions) : IInferenceClientFactory
{
    // The OpenAI SDK rejects an empty credential; servers started without an API key ignore the header.
    private const string NoApiKey = "unused";

    public IChatClient CreateChatClient(string httpClientName, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return new ReasoningFieldChatClient(CreateOpenAIClient(httpClientName).GetChatClient(model).AsIChatClient());
    }

    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(string httpClientName, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return CreateOpenAIClient(httpClientName).GetEmbeddingClient(model).AsIEmbeddingGenerator();
    }

    private OpenAIClient CreateOpenAIClient(string httpClientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(httpClientName);

        var registration = httpClientOptions.Get(httpClientName);
        if (registration.Endpoint is not { } endpointKind)
        {
            throw new InvalidOperationException($"HttpClient '{httpClientName}' was not registered with {nameof(InferenceServiceCollectionExtensions.AddInferenceHttpClient)}.");
        }

        var settings = options.Value;
        var endpoint = endpointKind == InferenceEndpoint.Chat ? settings.ChatEndpoint : settings.EmbeddingEndpoint;

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = ToApiBase(endpoint),
            Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(httpClientName)),
            // The named HttpClient's resilience handler owns retries and timeouts; the SDK's own retry
            // policy would replay non-idempotent generations, and its 100 s default network timeout would
            // cut long ones short.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = registration.NetworkTimeout,
        };

        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? NoApiKey : settings.ApiKey;

        return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
    }

    /// <summary>
    /// Maps a server root (<c>http://host:port</c>) to its OpenAI API base (<c>http://host:port/v1</c>).
    /// A root that already ends in <c>/v1</c> is kept as is.
    /// </summary>
    internal static Uri ToApiBase(Uri endpoint)
    {
        var root = endpoint.AbsoluteUri.TrimEnd('/');

        return root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? new Uri(root) : new Uri($"{root}/v1");
    }
}

/// <summary>
/// Per named HttpClient: which server it targets and the network timeout the OpenAI pipeline applies.
/// </summary>
internal sealed class InferenceHttpClientOptions
{
    public InferenceEndpoint? Endpoint { get; set; }

    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromMinutes(30);
}
