using System.Text.RegularExpressions;

namespace Sentium.Watchdog.Application.Monitoring.Probes;

public static partial class EndpointResolver
{
    public static (string Host, int Port)? ResolveHostPort(string? connectionString, int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        if (TryResolveUri(connectionString, out var uri))
        {
            return (uri!.Host, uri.Port > 0 ? uri.Port : defaultPort);
        }

        var server = ServerRegex().Match(connectionString);
        if (server.Success)
        {
            var value = server.Groups["v"].Value.Trim().Replace(',', ':');
            return SplitHostPort(value, defaultPort);
        }

        var firstToken = connectionString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        return SplitHostPort(firstToken, defaultPort);
    }

    public static Uri? ResolveUri(string? connectionString) => TryResolveUri(connectionString, out var uri) ? uri : null;

    private static bool TryResolveUri(string? connectionString, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var candidate = connectionString;

        var endpoint = EndpointRegex().Match(connectionString);
        if (endpoint.Success)
        {
            candidate = endpoint.Groups["v"].Value.Trim();
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out uri) && uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Host, int Port)? SplitHostPort(string value, int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            value = value[(schemeIndex + 3)..];
        }

        var slash = value.IndexOf('/');
        if (slash >= 0)
        {
            value = value[..slash];
        }

        var sep = value.LastIndexOf(':');
        if (sep > 0 && int.TryParse(value[(sep + 1)..], out var port))
        {
            return (value[..sep], port);
        }

        return (value, defaultPort);
    }

    [GeneratedRegex(@"(?:^|;)\s*Endpoint\s*=\s*(?<v>[^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EndpointRegex();

    [GeneratedRegex(@"(?:Server|Data Source)\s*=\s*(?<v>[^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ServerRegex();
}
