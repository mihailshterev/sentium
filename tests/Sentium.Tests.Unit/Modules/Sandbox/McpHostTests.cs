using System.Text;
using System.Threading.Channels;
using Docker.DotNet;
using FluentAssertions;
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

        config.Image.Should().Be(Image);
        config.Cmd.Should().Equal("--local-timezone=UTC");
        config.Env.Should().Contain("API_KEY=k").And.Contain("HOME=/tmp");
        config.OpenStdin.Should().BeTrue();
        config.AttachStdin.Should().BeTrue();
        config.StdinOnce.Should().BeFalse();
        config.Tty.Should().BeFalse("a TTY would merge stdout and stderr and corrupt the JSON-RPC stream");
        config.NetworkDisabled.Should().BeTrue();
        config.User.Should().BeNull("by default the image's own user runs the server");
        config.Labels.Should().Contain(ContainerConfigBuilder.McpServerLabel, "time");

        var host = config.HostConfig;
        host.NetworkMode.Should().Be("none");
        host.ReadonlyRootfs.Should().BeTrue();
        host.CapDrop.Should().Equal("ALL");
        host.Privileged.Should().BeFalse();
        host.SecurityOpt.Should().Contain("no-new-privileges:true");
        host.Memory.Should().Be(512L * 1024 * 1024, "requests are clamped to the operator maximum");
        host.NanoCPUs.Should().Be(1_000_000_000L);
        host.Mounts.Should().BeNullOrEmpty("an MCP server gets no host directory");
    }

    [Fact]
    public void BuildMcp_ForcesTheOperatorsUser_WhenConfigured()
    {
        var config = CreateBuilder(new McpHostOptions { RunAsUser = "65534:65534" }).BuildMcp("time", new McpServerLaunchSpec(Image, null, null, null, null, null));

        config.User.Should().Be("65534:65534");
    }

    [Fact]
    public void BuildMcp_JoinsANamedNetwork_WhenTheSpecHasOne()
    {
        var config = CreateBuilder(new McpHostOptions()).BuildMcp("search", new McpServerLaunchSpec(Image, null, null, "sentium-egress", null, null));

        config.NetworkDisabled.Should().BeFalse();
        config.HostConfig.NetworkMode.Should().Be("sentium-egress");
        config.Cmd.Should().BeNull("an empty argument list keeps the image's default command");
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

        actualName.Should().Be(name);
        actualTag.Should().Be(tag);
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

        (await act.Should().ThrowAsync<McpHostRejectedException>()).Which.Message.Should().Contain(expected);
        docker.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownServer_HasNoToolsAndNoStatus()
    {
        await using var manager = CreateManager(Substitute.For<IDockerClient>(), new McpHostOptions { Enabled = true });

        (await manager.ListToolsAsync("missing", Ct)).Should().BeNull();
        manager.GetStatus("missing").Should().BeNull();
        (await manager.StopAsync("missing")).Should().BeFalse();
    }

    [Fact]
    public async Task Bridge_PassesStdoutThrough_KeepsAStderrTail_AndForwardsStdin()
    {
        using var stdio = new FakeContainerStdio();
        await using var bridge = new ContainerStdioBridge(stdio, maxMessageBytes: 1_024, stderrLines: 2);

        stdio.Emit(StdioTarget.Stderr, "starting\nline two\nline three\n");
        stdio.Emit(StdioTarget.Stdout, "{\"jsonrpc\":\"2.0\",\"id\":1}\n");

        using var reader = new StreamReader(bridge.ServerOutput, Encoding.UTF8, leaveOpen: true);
        (await reader.ReadLineAsync(Ct)).Should().Be("{\"jsonrpc\":\"2.0\",\"id\":1}");

        bridge.StderrTail.Should().Equal("line two", "line three");

        await bridge.ServerInput.WriteAsync(Encoding.UTF8.GetBytes("ping\n"), Ct);
        stdio.Written.ToString().Should().Be("ping\n");

        stdio.End();
        await bridge.Completion.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        (await reader.ReadLineAsync(Ct)).Should().BeNull("the container's EOF ends the client's input");
    }

    [Fact]
    public async Task Bridge_FailsTheSession_WhenAMessageExceedsTheCap()
    {
        using var stdio = new FakeContainerStdio();
        await using var bridge = new ContainerStdioBridge(stdio, maxMessageBytes: 16, stderrLines: 5);

        stdio.Emit(StdioTarget.Stdout, new string('x', 64));

        await bridge.Completion.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        bridge.StderrTail.Should().ContainSingle(l => l.Contains("larger than 16 bytes"));

        var read = async () =>
        {
            using var reader = new StreamReader(bridge.ServerOutput, Encoding.UTF8, leaveOpen: true);
            await reader.ReadToEndAsync(Ct);
        };
        await read.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Bridge_DisposingClosesTheContainersStdin()
    {
        using var stdio = new FakeContainerStdio();
        var bridge = new ContainerStdioBridge(stdio, 1_024, 5);

        await bridge.DisposeAsync();

        stdio.InputClosed.Should().BeTrue();
        stdio.Disposed.Should().BeTrue();
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

        stdout.ToString().Should().Be("{\"id\":1}\ntail");
        stderr.ToString().Should().Be("warning\n");
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
