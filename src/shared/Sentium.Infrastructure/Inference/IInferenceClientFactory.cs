using Microsoft.Extensions.AI;

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
