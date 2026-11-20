using System.Text;
using System.Threading.Channels;
using Docker.DotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Mcp;
using Sentium.Sandbox.Options;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

/// <summary>
/// The Sandbox's MCP host: hardened container config, operator allow-lists enforced next to the Docker socket, image
/// reference parsing, and the stdio bridge between Docker's attach stream and the MCP SDK.
/// </summary>
public sealed class McpHostTests
{
    private const string Image = "mcp/time@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void BuildMcp_IsHardened_HasAnAttachableStdin_AndNoNetworkByDefault()
    {
        var builder = CreateBuilder(new McpHostOptions { MaxMemoryMb = 512, MaxCpuLimit = 1 });

        var config = builder.BuildMcp("time", new McpServerLaunchSpec(Image, ["--local-timezone=UTC"], new Dictionary<string, string> { ["API_KEY"] = "k" }, null, 4_096, 4));

        Assert.Equal(Image, config.Image);
        Assert.Equal(new string[] { "--local-timezone=UTC" }, config.Cmd);
        Assert.Contains("API_KEY=k", config.Env);
        Assert.Contains("HOME=/tmp", config.Env);
        Assert.True(config.OpenStdin);
        Assert.True(config.AttachStdin);
        Assert.False(config.StdinOnce);
        Assert.False(config.Tty, "a TTY would merge stdout and stderr and corrupt the JSON-RPC stream");
        Assert.True(config.NetworkDisabled);
        // by default the image's own user runs the server
        Assert.Null(config.User);
        Assert.Equal("time", Assert.Contains(ContainerConfigBuilder.McpServerLabel, config.Labels));

        var host = config.HostConfig!;
        Assert.Equal("none", host.NetworkMode);
        Assert.True(host.ReadonlyRootfs);
        Assert.Equal(new string[] { "ALL" }, host.CapDrop);
        Assert.False(host.Privileged);
        Assert.Contains("no-new-privileges:true", host.SecurityOpt);
        // requests are clamped to the operator maximum
        Assert.Equal(512L * 1024 * 1024, host.Memory);
        Assert.Equal(1_000_000_000L, host.NanoCPUs);
        Assert.True(host.Mounts is null || host.Mounts.Count == 0, "an MCP server gets no host directory");
    }

    [Fact]
    public void BuildMcp_ForcesTheOperatorsUser_WhenConfigured()
    {
        var config = CreateBuilder(new McpHostOptions { RunAsUser = "65534:65534" }).BuildMcp("time", new McpServerLaunchSpec(Image, null, null, null, null, null));

        Assert.Equal("65534:65534", config.User);
    }

    [Fact]
    public void BuildMcp_JoinsANamedNetwork_WhenTheSpecHasOne()
    {
        var config = CreateBuilder(new McpHostOptions()).BuildMcp("search", new McpServerLaunchSpec(Image, null, null, "sentium-egress", null, null));

        Assert.False(config.NetworkDisabled);
        Assert.Equal("sentium-egress", config.HostConfig!.NetworkMode);
        // an empty argument list keeps the image's default command
        Assert.Null(config.Cmd);
    }

    [Theory]
    [InlineData("python:3.12-slim", "python", "3.12-slim")]
    [InlineData("node", "node", "latest")]
    [InlineData("localhost:5000/team/img", "localhost:5000/team/img", "latest")]
    [InlineData("localhost:5000/team/img:1.2", "localhost:5000/team/img", "1.2")]
    [InlineData(Image, Image, null)]
    public void SplitImageRef_HandlesRegistryPortsAndDigests(string reference, string name, string? tag)
    {
        var (actualName, actualTag) = DockerImageService.SplitImageRef(reference);

        Assert.Equal(name, actualName);
        Assert.Equal(tag, actualTag);
    }

    [Theory]
    [InlineData(false, new string[0], Image, null, "disabled")]
    [InlineData(true, new string[0], "mcp/time:latest", null, "digest")]
    [InlineData(true, new[] { "sentium-egress" }, Image, "bridge", "Network 'bridge'")]
    public async Task Ensure_EnforcesTheOperatorLimits_BeforeTouchingDocker(
        bool enabled, string[] allowedNetworks, string image, string? network, string expected)
    {
        var docker = Substitute.For<IDockerClient>();
        var options = new McpHostOptions { Enabled = enabled, AllowedNetworks = [.. allowedNetworks] };
        await using var manager = CreateManager(docker, options);

        var act = () => manager.EnsureAsync("time", new McpServerLaunchSpec(image, null, null, network, null, null), Ct);

        Assert.Contains(expected, (await Assert.ThrowsAnyAsync<McpHostRejectedException>(act)).Message, StringComparison.Ordinal);
        Assert.Empty(docker.ReceivedCalls());
    }

    [Fact]
    public async Task UnknownServer_HasNoToolsAndNoStatus()
    {
        await using var manager = CreateManager(Substitute.For<IDockerClient>(), new McpHostOptions { Enabled = true });

        Assert.Null(await manager.ListToolsAsync("missing", Ct));
        Assert.Null(manager.GetStatus("missing"));
        Assert.False(await manager.StopAsync("missing"));
    }

    [Fact]
    public async Task Bridge_PassesStdoutThrough_KeepsAStderrTail_AndForwardsStdin()
    {
        using var stdio = new FakeContainerStdio();
        await using var bridge = new ContainerStdioBridge(stdio, maxMessageBytes: 1_024, stderrLines: 2);

        stdio.Emit(StdioTarget.Stderr, "starting\nline two\nline three\n");
        stdio.Emit(StdioTarget.Stdout, "{\"jsonrpc\":\"2.0\",\"id\":1}\n");

        using var reader = new StreamReader(bridge.ServerOutput, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("{\"jsonrpc\":\"2.0\",\"id\":1}", await reader.ReadLineAsync(Ct));

        Assert.Equal(new string[] { "line two", "line three" }, bridge.StderrTail);

        await bridge.ServerInput.WriteAsync(Encoding.UTF8.GetBytes("ping\n"), Ct);
        Assert.Equal("ping\n", stdio.Written.ToString());

        stdio.End();
        await bridge.Completion.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        // the container's EOF ends the client's input
        Assert.Null(await reader.ReadLineAsync(Ct));
    }

    [Fact]
    public async Task Bridge_FailsTheSession_WhenAMessageExceedsTheCap()
    {
        using var stdio = new FakeContainerStdio();
        await using var bridge = new ContainerStdioBridge(stdio, maxMessageBytes: 16, stderrLines: 5);

        stdio.Emit(StdioTarget.Stdout, new string('x', 64));

        await bridge.Completion.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Single(bridge.StderrTail, l => l.Contains("larger than 16 bytes"));

        var read = async () =>
        {
            using var reader = new StreamReader(bridge.ServerOutput, Encoding.UTF8, leaveOpen: true);
            await reader.ReadToEndAsync(Ct);
        };
        await Assert.ThrowsAnyAsync<InvalidDataException>(read);
    }

    [Fact]
    public async Task Bridge_DisposingClosesTheContainersStdin()
    {
        using var stdio = new FakeContainerStdio();
        var bridge = new ContainerStdioBridge(stdio, 1_024, 5);

        await bridge.DisposeAsync();

        Assert.True(stdio.InputClosed);
        Assert.True(stdio.Disposed);
    }

    [Fact]
    public async Task HijackedStdio_DemultiplexesDockerFrames_AcrossReads()
    {
        using var raw = new MemoryStream();
        WriteFrame(raw, 1, "{\"id\":1}\n");
        WriteFrame(raw, 2, "warning\n");
        WriteFrame(raw, 1, "tail");
        raw.Position = 0;

        using var stdio = new HijackedContainerStdio(raw, socket: null);
        var buffer = new byte[4];
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        while (true)
        {
            var chunk = await stdio.ReadAsync(buffer, Ct);
            if (chunk.EndOfStream)
            {
                break;
            }

            (chunk.Target == StdioTarget.Stderr ? stderr : stdout).Append(Encoding.UTF8.GetString(buffer, 0, chunk.Count));
        }

        Assert.Equal("{\"id\":1}\ntail", stdout.ToString());
        Assert.Equal("warning\n", stderr.ToString());
    }

    private static void WriteFrame(Stream stream, byte target, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var header = new byte[8];
        header[0] = target;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)payload.Length);
        stream.Write(header);
        stream.Write(payload);
    }

    private static ContainerConfigBuilder CreateBuilder(McpHostOptions mcp) =>
        new(Options.Create(new SandboxOptions()), Options.Create(mcp));

    private static McpContainerManager CreateManager(IDockerClient docker, McpHostOptions mcp)
    {
        return new(
            docker,
            new DockerStdioAttach(Options.Create(new SandboxOptions())),
            new DockerImageService(docker, NullLogger<DockerImageService>.Instance),
            CreateBuilder(mcp),
            Options.Create(mcp),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            NullLogger<McpContainerManager>.Instance);
    }

    private sealed class FakeContainerStdio : IContainerStdio
    {
        private readonly Channel<(StdioTarget Target, byte[]? Data)> output = Channel.CreateUnbounded<(StdioTarget, byte[]?)>();

        public StringBuilder Written { get; } = new();

        public bool InputClosed { get; private set; }

        public bool Disposed { get; private set; }

        public void Emit(StdioTarget target, string text) => output.Writer.TryWrite((target, Encoding.UTF8.GetBytes(text)));

        public void End() => output.Writer.TryWrite((StdioTarget.Stdout, null));

        public async Task<StdioChunk> ReadAsync(byte[] buffer, CancellationToken ct)
        {
            var (target, data) = await output.Reader.ReadAsync(ct);
            if (data is null)
            {
                return new StdioChunk(0, target, EndOfStream: true);
            }

            data.CopyTo(buffer, 0);
            return new StdioChunk(data.Length, target, EndOfStream: false);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            Written.Append(Encoding.UTF8.GetString(buffer, offset, count));
            return Task.CompletedTask;
        }

        public void CloseInput() => InputClosed = true;

        public void Dispose()
        {
            Disposed = true;
            output.Writer.TryComplete();
        }
    }
}
