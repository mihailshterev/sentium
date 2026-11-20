using Docker.DotNet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Sandbox.Artifacts;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Models;
using Sentium.Sandbox.Options;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

/// <summary>
/// The sandbox runner against a real Docker daemon: pull, create, attach, start, wait, demultiplex and clean up.
/// </summary>
/// <remarks>
/// These start real containers, so they are explicit and stay out of the regular unit run. Run them with
/// <c>dotnet test --project tests/Sentium.Tests.Unit --explicit only</c> after touching the Docker client or its usage.
/// </remarks>
public sealed class DockerSandboxRunnerTests : IDisposable
{
    private readonly SandboxOptions _options = new() { ExecutionTimeoutSeconds = 20 };
    private readonly IDockerClient _docker;

    public DockerSandboxRunnerTests()
    {
        _docker = new DockerClientBuilder().WithEndpoint(DockerEndpoint.Resolve(_options)).Build();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _docker.Dispose();

    [Fact(Explicit = true)]
    public async Task Python_WritesStdout_AndExitsCleanly()
    {
        var result = await RunAsync("print('hello from the sandbox')");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello from the sandbox", result.Output.Trim());
        Assert.Empty(result.Error);
        Assert.False(result.TimedOut);
    }

    [Fact(Explicit = true)]
    public async Task Python_SeparatesStderr_AndReportsTheExitCode()
    {
        var result = await RunAsync("import sys\nprint('out')\nprint('broken', file=sys.stderr)\nsys.exit(3)");

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("out", result.Output.Trim());
        Assert.Contains("broken", result.Error, StringComparison.Ordinal);
    }

    [Fact(Explicit = true)]
    public async Task Python_HasNoNetwork()
    {
        var result = await RunAsync("import socket\nsocket.create_connection(('1.1.1.1', 53), timeout=3)");

        Assert.False(result.Succeeded);
        Assert.Contains("OSError", result.Error, StringComparison.Ordinal);
    }

    [Fact(Explicit = true)]
    public async Task Python_IsStoppedAtTheTimeout()
    {
        _options.ExecutionTimeoutSeconds = 2;

        var result = await RunAsync("while True:\n    pass");

        Assert.True(result.TimedOut);
        Assert.False(result.Succeeded);
    }

    private async Task<ExecutionResult> RunAsync(string code)
    {
        var options = Options.Create(_options);
        var artifacts = Substitute.For<IArtifactService>();
        artifacts.HarvestAsync(default!, default!, default, default).ReturnsForAnyArgs([]);

        var runner = new DockerSandboxRunner(
            _docker,
            new JobDirectoryService(options, NullLogger<JobDirectoryService>.Instance),
            new ContainerConfigBuilder(options, Options.Create(new McpHostOptions())),
            new DockerImageService(_docker, NullLogger<DockerImageService>.Instance),
            artifacts,
            options,
            NullLogger<DockerSandboxRunner>.Instance);

        return await runner.RunAsync(
            new ExecutionRequest
            {
                Language = ExecutionLanguage.Python,
                Code = code,
                AgentId = "test-agent",
                CorrelationId = Guid.NewGuid().ToString(),
            },
            Ct);
    }
}
