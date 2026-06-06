using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Lightweight text-similarity utilities used by the agentic refinement loop to detect a stuck state -
/// when a local model ignores the reviewer's critique and emits the same (or near-identical) output on
/// consecutive turns. Detecting this lets the loop abort early and conserve local compute/VRAM.
/// </summary>
public static partial class OutputSimilarity
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex PunctuationRegex();

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var stripped = PunctuationRegex().Replace(text, " ");
        return WhitespaceRegex().Replace(stripped, " ").Trim().ToLowerInvariant();
    }

    public static string ComputeHash(string? text)
    {
        var normalized = Normalize(text);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Word-set Jaccard index (|A ∩ B| / |A ∪ B|) over the normalized token sets.
    /// Returns 1.0 for identical content and 0.0 when there is no overlap. Two empty inputs are treated as identical.
    /// </summary>
    public static double Jaccard(string? a, string? b)
    {
        var setA = new HashSet<string>(Normalize(a).Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        var setB = new HashSet<string>(Normalize(b).Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

        if (setA.Count == 0 && setB.Count == 0)
        {
            return 1.0;
        }

        if (setA.Count == 0 || setB.Count == 0)
        {
            return 0.0;
        }

        var intersection = setA.Count(setB.Contains);
        var union = setA.Count + setB.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }

    /// <summary>
    /// True when two consecutive squad outputs are identical or highly similar (Jaccard ≥ <paramref name="threshold"/>),
    /// indicating the loop is stuck and should abort.
    /// </summary>
    public static bool IsStuck(string? previous, string? current, double threshold = 0.95)
    {
        if (previous is null)
        {
            return false;
        }

        return string.Equals(ComputeHash(previous), ComputeHash(current), StringComparison.Ordinal) || Jaccard(previous, current) >= threshold;
    }
}
