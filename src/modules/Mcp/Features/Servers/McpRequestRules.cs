using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;
using Sentium.Infrastructure.Net;
using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

public static partial class McpRequestRules
{
    private const int MaxSecrets = 32;
    private const int MaxSecretValueLength = 8_192;

    /// <summary>
    /// Headers Sentium controls itself or that would let a server config tamper with the request framing.
    /// </summary>
    private static readonly HashSet<string> ReservedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Cookie", "Content-Length", "Content-Type", "Transfer-Encoding", "Connection", "Upgrade", "TE", "Trailer",
        "Keep-Alive", "Proxy-Authorization", "Proxy-Connection", "Accept", "Mcp-Session-Id", "Mcp-Protocol-Version",
        "Last-Event-ID", "X-Internal-Token",
    };

    public static IRuleBuilderOptions<T, string?> McpEndpoint<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .NotEmpty().WithMessage("An HTTP MCP server needs an endpoint.")
            .MaximumLength(2048)
            .Must(BeLocalHttpUrl)
            .WithMessage("The endpoint must be an http(s) URL on the local network (loopback or a private address range); public internet endpoints are not allowed.");
    }

    public static IRuleBuilderOptions<T, string?> McpImage<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .NotEmpty().WithMessage("A Sandbox MCP server needs a container image.")
            .MaximumLength(512)
            .Matches(DigestPinnedImage())
            .WithMessage("The image must be pinned by digest, e.g. 'mcp/time@sha256:<64 hex characters>'.");
    }

    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> McpArgs<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule)
    {
        return rule
            .Must(args => args is null || (args.Count <= 64 && args.All(a => a is not null && a.Length <= 1024)))
            .WithMessage("At most 64 arguments of up to 1024 characters each.");
    }

    public static IRuleBuilderOptions<T, string?> McpNetwork<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .Must(network => string.IsNullOrWhiteSpace(network) || DockerNetworkName().IsMatch(network))
            .WithMessage("The network must be a Docker network name.");
    }

    public static bool SecretsAreValid(McpTransport transport, IEnumerable<KeyValuePair<string, string?>>? secrets)
    {
        if (secrets is null)
        {
            return true;
        }

        var list = secrets.ToList();
        if (list.Count > MaxSecrets)
        {
            return false;
        }

        foreach (var (name, value) in list)
        {
            var validName = transport == McpTransport.Http
                ? HeaderName().IsMatch(name) && !ReservedHeaders.Contains(name)
                : EnvironmentName().IsMatch(name);

            if (!validName || (value is not null && (value.Length > MaxSecretValueLength || value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The transport-specific checks of an update, against the stored server's transport; <see langword="null"/> when valid.
    /// </summary>
    public static string? ValidateForTransport(McpTransport transport, UpdateMcpServerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (transport == McpTransport.Http && string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return "An HTTP MCP server needs an endpoint.";
        }

        if (transport == McpTransport.SandboxStdio && string.IsNullOrWhiteSpace(request.Image))
        {
            return "A Sandbox MCP server needs a container image.";
        }

        if (!SecretsAreValid(transport, request.Secrets) || !SecretsAreValid(transport, request.Variables))
        {
            return SecretsMessage(transport);
        }

        return NamesOverlap(request.Secrets?.Keys, request.Variables?.Keys) ? "A name is either a secret or a plain variable, not both." : null;
    }

    public static bool NamesOverlap(IEnumerable<string>? first, IEnumerable<string>? second) =>
        first is not null && second is not null && first.Intersect(second, StringComparer.OrdinalIgnoreCase).Any();

    public static string SecretsMessage(McpTransport transport)
    {
        return transport == McpTransport.Http
            ? $"Secrets are HTTP headers: at most {MaxSecrets}, valid header names (not Host, Cookie, X-Internal-Token or other transport headers) and single-line values."
            : $"Secrets are environment variables: at most {MaxSecrets}, names like MY_API_KEY and single-line values.";
    }

    private static bool BeLocalHttpUrl(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // Literal IPs are judged now; host names are checked again (on the resolved address) every time we connect.
        return !IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) || NetworkAddressPolicy.IsAllowed(address, NetworkAddressScope.PrivateOnly);
    }

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9._/:-]*[a-z0-9])?@sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPinnedImage();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex DockerNetworkName();

    [GeneratedRegex(@"^[A-Za-z0-9!#$%&'*+.^_`|~-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderName();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentName();
}
