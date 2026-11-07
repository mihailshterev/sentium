namespace Sentium.Codex.Contracts;

/// <summary>
/// Generates a dense vector embedding for a piece of text.
/// The infrastructure implementation calls the embedding server (llama-server) through Microsoft.Extensions.AI.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Produces a float vector for <paramref name="text"/>.
    /// The vector dimensionality must match the vector store column (<c>CodexDbContext.EmbeddingDimensions</c>, 768).
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
