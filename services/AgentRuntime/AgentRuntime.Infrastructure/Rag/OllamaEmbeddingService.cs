using AgentRuntime.Core.Rag;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentRuntime.Infrastructure.Rag;

/// <summary>
/// Generates text embeddings by delegating to the Ollama embedding model configured
/// via <c>appsettings.json → Rag:EmbeddingModelName</c>.
/// Uses <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> from Microsoft.Extensions.AI,
/// which OllamaSharp implements natively, ensuring consistent OpenTelemetry tracing.
/// </summary>
public sealed class OllamaEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<OllamaEmbeddingService> logger) : IEmbeddingService
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
