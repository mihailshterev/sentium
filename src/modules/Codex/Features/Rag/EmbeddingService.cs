using Sentium.Codex.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Sentium.Codex.Features.Rag;

/// <summary>
/// Generates text embeddings with the embedding model configured via <c>Rag:EmbeddingModelName</c>,
/// served by the inference server's OpenAI-compatible <c>/v1/embeddings</c> endpoint.
/// Uses <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> from Microsoft.Extensions.AI, wrapped
/// with OpenTelemetry so embedding calls are traced consistently.
/// </summary>
public sealed class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<EmbeddingService> logger) : IEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Generating embedding for {CharCount} characters", text.Length);
        }

        var result = await embeddingGenerator.GenerateAsync([text], cancellationToken: ct);

        if (result.Count == 0)
        {
            throw new InvalidOperationException("Embedding generator returned an empty result set.");
        }

        return result[0].Vector.ToArray();
    }
}
