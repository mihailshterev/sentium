using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;
using Sentium.Infrastructure.Net;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Mcp;

namespace Sentium.Praxis.Validation;

public sealed class CreateMcpServerRequestValidator : AbstractValidator<CreateMcpServerRequest>
{
    public CreateMcpServerRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(McpNames.IsValidServerName)
            .WithMessage("Name must be 2-24 characters: lowercase letters, digits and '-', starting with a letter or digit.");

        RuleFor(x => x.Transport).IsInEnum();

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);

        When(x => x.Transport == McpTransport.Http, () =>
            RuleFor(x => x.Endpoint).McpEndpoint());

        When(x => x.Transport == McpTransport.SandboxStdio, () =>
        {
            RuleFor(x => x.Image).McpImage();
            RuleFor(x => x.Args).McpArgs();
            RuleFor(x => x.Network).McpNetwork();
        });

        RuleFor(x => x.MemoryMb).InclusiveBetween(32, 8_192).When(x => x.MemoryMb.HasValue);
        RuleFor(x => x.CpuLimit).InclusiveBetween(0.1, 8).When(x => x.CpuLimit.HasValue);

        RuleFor(x => x.Secrets)
            .Must((request, secrets) => McpRequestRules.SecretsAreValid(request.Transport, secrets?.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))))
            .WithMessage(x => McpRequestRules.SecretsMessage(x.Transport));

        RuleFor(x => x.Variables)
            .Must((request, variables) => McpRequestRules.SecretsAreValid(request.Transport, variables?.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))))
            .WithMessage(x => McpRequestRules.SecretsMessage(x.Transport));

        RuleFor(x => x)
            .Must(x => !McpRequestRules.NamesOverlap(x.Secrets?.Keys, x.Variables?.Keys))
            .WithMessage("A name is either a secret or a plain variable, not both.");
    }
}

/// <remarks>
/// The transport cannot change after creation and is not part of the request, so the transport-specific requirements
/// (an endpoint vs. an image, header vs. environment-variable secret names) are checked by the service against the
/// stored server (<see cref="McpRequestRules.ValidateForTransport"/>); this validator checks the shapes.
/// </remarks>
public sealed class UpdateMcpServerRequestValidator : AbstractValidator<UpdateMcpServerRequest>
{
    public UpdateMcpServerRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);

        When(x => !string.IsNullOrWhiteSpace(x.Endpoint), () => RuleFor(x => x.Endpoint).McpEndpoint());
        When(x => !string.IsNullOrWhiteSpace(x.Image), () => RuleFor(x => x.Image).McpImage());

        RuleFor(x => x.Args).McpArgs();
        RuleFor(x => x.Network).McpNetwork();

        RuleFor(x => x.MemoryMb).InclusiveBetween(32, 8_192).When(x => x.MemoryMb.HasValue);
        RuleFor(x => x.CpuLimit).InclusiveBetween(0.1, 8).When(x => x.CpuLimit.HasValue);
    }
}

public sealed class UpdateMcpToolsRequestValidator : AbstractValidator<UpdateMcpToolsRequest>
{
    public UpdateMcpToolsRequestValidator()
    {
        RuleFor(x => x.Tools).NotNull().Must(t => t.Count <= 500).WithMessage("Too many tool updates.");
        RuleForEach(x => x.Tools).ChildRules(tool => tool.RuleFor(t => t.Name).NotEmpty().MaximumLength(256));
    }
}

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

    public static IRuleBuilderOptions<T, string?> McpEndpoint<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty().WithMessage("An HTTP MCP server needs an endpoint.")
            .MaximumLength(2048)
            .Must(BeLocalHttpUrl)
            .WithMessage("The endpoint must be an http(s) URL on the local network (loopback or a private address range); public internet endpoints are not allowed.");

    public static IRuleBuilderOptions<T, string?> McpImage<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty().WithMessage("A Sandbox MCP server needs a container image.")
            .MaximumLength(512)
            .Matches(DigestPinnedImage())
            .WithMessage("The image must be pinned by digest, e.g. 'mcp/time@sha256:<64 hex characters>'.");

    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> McpArgs<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule
            .Must(args => args is null || (args.Count <= 64 && args.All(a => a is not null && a.Length <= 1024)))
            .WithMessage("At most 64 arguments of up to 1024 characters each.");

    public static IRuleBuilderOptions<T, string?> McpNetwork<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(network => string.IsNullOrWhiteSpace(network) || DockerNetworkName().IsMatch(network))
            .WithMessage("The network must be a Docker network name.");

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

    public static string SecretsMessage(McpTransport transport) => transport == McpTransport.Http
        ? $"Secrets are HTTP headers: at most {MaxSecrets}, valid header names (not Host, Cookie, X-Internal-Token or other transport headers) and single-line values."
        : $"Secrets are environment variables: at most {MaxSecrets}, names like MY_API_KEY and single-line values.";

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
