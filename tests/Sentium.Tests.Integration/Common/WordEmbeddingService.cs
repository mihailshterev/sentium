using Sentium.Codex.Contracts;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// A deterministic stand-in for the embedding server: a bag-of-words embedding, so texts sharing most of their words
/// score close to 1 and unrelated texts close to 0. Case and punctuation are ignored.
/// </summary>
internal sealed class WordEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 768;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        return Task.FromResult(Embed(text));
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<float[]>>([.. texts.Select(Embed)]);
    }

    private static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var words = text.ToLowerInvariant()
            .Split([' ', '.', ',', '!', '?', '\n', '*', ':', '#'], StringSplitOptions.RemoveEmptyEntries)
            .Distinct();

        foreach (var word in words)
        {
            vector[StableHash(word) % Dimensions] += 1f;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return norm == 0 ? vector : [.. vector.Select(v => v / norm)];
    }

    private static int StableHash(string word)
    {
        var hash = 17;
        foreach (var c in word)
        {
            hash = unchecked((hash * 31) + c);
        }

        return hash & int.MaxValue;
    }
}
