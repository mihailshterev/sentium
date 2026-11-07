using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Sentium.Infrastructure.Extensions;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Reads a positive duration given either as a <see cref="TimeSpan"/> (<c>00:30:00</c>) or as a number of minutes.
    /// Falls back to <paramref name="fallback"/> when the value is missing, malformed, or not positive.
    /// </summary>
    public static TimeSpan GetTimeSpan(this IConfiguration configuration, string key, TimeSpan fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var asTimeSpan) && asTimeSpan > TimeSpan.Zero)
        {
            return asTimeSpan;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : fallback;
    }
}
