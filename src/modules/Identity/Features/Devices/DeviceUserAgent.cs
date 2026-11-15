namespace Sentium.Identity.Features.Devices;

/// <summary>
/// The Satelles app identifies itself as <c>SentiumSatelles/&lt;version&gt;</c> in its User-Agent, which is where a
/// Device's app version comes from.
/// </summary>
internal static class DeviceUserAgent
{
    public const string ProductName = "SentiumSatelles";

    private const string ProductPrefix = ProductName + "/";

    public static string? ParseAppVersion(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var product = userAgent
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(p => p.StartsWith(ProductPrefix, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            return null;
        }

        var version = product[ProductPrefix.Length..];
        var isPlainVersion = version.Length is > 0 and <= DeviceLimits.AppVersionMax
            && version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+');

        return isPlainVersion ? version : null;
    }
}
