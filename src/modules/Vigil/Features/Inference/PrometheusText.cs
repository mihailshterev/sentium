using System.Globalization;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Reads the unlabelled samples of a Prometheus text exposition (<c>name value [timestamp]</c>), which is all
/// llama-server's <c>/metrics</c> needs. Comments, labelled samples and values that are not finite are skipped.
/// </summary>
internal static class PrometheusText
{
    public static IReadOnlyDictionary<string, double> ParseUnlabelled(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var samples = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var rawLine in text.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == '#' || line.Contains('{'))
            {
                continue;
            }

            var separator = line.IndexOfAny(' ', '\t');
            if (separator <= 0)
            {
                continue;
            }

            var rest = line[(separator + 1)..].TrimStart();
            var valueEnd = rest.IndexOfAny(' ', '\t');
            var value = valueEnd < 0 ? rest : rest[..valueEnd];

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                samples[line[..separator].ToString()] = number;
            }
        }

        return samples;
    }
}
