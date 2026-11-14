using FluentAssertions;
using Sentium.Mcp.Entities;
using Sentium.Mcp.Features.Servers;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Mcp;

public sealed class McpServerRequestValidatorTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly CreateMcpServerRequestValidator _validator = new();

    [Theory]
    [InlineData("http://192.168.1.10:8123/mcp", true)]
    [InlineData("http://localhost:3000/mcp", true)]
    [InlineData("http://homeassistant.local:8123/mcp", true)]
    [InlineData("https://[fd00::5]/mcp", true)]
    [InlineData("http://8.8.8.8/mcp", false)]
    [InlineData("http://169.254.169.254/latest", false)]
    [InlineData("ftp://192.168.1.10/mcp", false)]
    [InlineData("http://user:pass@192.168.1.10/mcp", false)]
    [InlineData("not a url", false)]
    public void HttpEndpoint_MustBeAnHttpUrlOnTheLocalNetwork(string endpoint, bool valid)
    {
        var result = _validator.Validate(Http(endpoint));

        result.IsValid.Should().Be(valid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("mcp/time@sha256:" + Digest, true)]
    [InlineData("ghcr.io/acme/mcp-searxng@sha256:" + Digest, true)]
    [InlineData("mcp/time:latest", false)]
    [InlineData("mcp/time", false)]
    public void SandboxImage_MustBePinnedByDigest(string image, bool valid) =>
        _validator.Validate(Stdio(image)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData("Authorization", true)]
    [InlineData("X-Api-Key", true)]
    [InlineData("Host", false)]
    [InlineData("Cookie", false)]
    [InlineData("X-Internal-Token", false)]
    [InlineData("Mcp-Session-Id", false)]
    [InlineData("Bad Header", false)]
    public void HttpSecrets_AreHeaders_NotReservedTransportHeaders(string header, bool valid)
    {
        _validator.Validate(Http("http://192.168.1.10/mcp") with { Secrets = new Dictionary<string, string> { [header] = "value" } })
            .IsValid.Should().Be(valid);
    }

    [Fact]
    public void Secrets_RejectMultiLineValues_AndStdioSecretsMustBeEnvironmentNames()
    {
        _validator.Validate(Http("http://192.168.1.10/mcp") with { Secrets = new Dictionary<string, string> { ["Authorization"] = "a\r\nX-Injected: 1" } })
            .IsValid.Should().BeFalse();

        _validator.Validate(Stdio("mcp/time@sha256:" + Digest) with { Secrets = new Dictionary<string, string> { ["API_KEY"] = "v" } })
            .IsValid.Should().BeTrue();

        _validator.Validate(Stdio("mcp/time@sha256:" + Digest) with { Secrets = new Dictionary<string, string> { ["api-key"] = "v" } })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_ChecksTransportSpecificRequirements_AgainstTheStoredTransport()
    {
        var update = new UpdateMcpServerRequest("Docs", null, Endpoint: null, Image: "mcp/time@sha256:" + Digest, Args: null, Network: null, MemoryMb: null, CpuLimit: null, Secrets: null, Enabled: true);

        McpRequestRules.ValidateForTransport(McpTransport.Http, update).Should().Contain("endpoint");
        McpRequestRules.ValidateForTransport(McpTransport.SandboxStdio, update).Should().BeNull();
    }

    private static CreateMcpServerRequest Http(string endpoint) =>
        new("docs", "Docs", null, McpTransport.Http, endpoint, null, null, null, null, null, null);

    private static CreateMcpServerRequest Stdio(string image) =>
        new("time", "Time", null, McpTransport.SandboxStdio, null, image, ["--local-timezone=Europe/Sofia"], null, null, null, null);
}
