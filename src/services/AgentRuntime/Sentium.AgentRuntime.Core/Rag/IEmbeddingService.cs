namespace Sentium.AgentRuntime.Core.Rag;

/// <summary>
/// Generates a dense vector embedding for a piece of text.
/// The infrastructure implementation delegates to an Ollama embedding model.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Produces a float vector for <paramref name="text"/>.
    /// The vector dimensionality must match <see cref="RagOptions.VectorSize"/>.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
