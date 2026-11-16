namespace Sentium.Codex.Contracts.Models;

public sealed record EmbeddedChunk(DocumentChunk Chunk, float[] Embedding);
