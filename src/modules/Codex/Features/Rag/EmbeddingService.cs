using Sentium.Codex.Contracts;
using Microsoft.Extensions.AI;

namespace Sentium.Codex.Features.Rag;

/// <summary>
/// Generates text embeddings with the embedding model configured via <c>Rag:EmbeddingModelName</c>,
/// served by the inference server's OpenAI-compatible <c>/v1/embeddings</c> endpoint.
/// Uses <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> from Microsoft.Extensions.AI, wrapped
/// with OpenTelemetry so embedding calls are traced consistently.
/// </summary>
internal sealed class EmbeddingService(
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

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        var result = await embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);

        if (result.Count != texts.Count)
        {
            throw new InvalidOperationException($"The embedding model returned {result.Count} vectors for {texts.Count} texts.");
        }

        return [.. result.Select(e => e.Vector.ToArray())];
    }
}
