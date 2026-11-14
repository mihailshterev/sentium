using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Sentinel;
using Sentium.Sandbox.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

public sealed class SandboxOrchestratorTests
{
    private readonly ISandboxRunner _runner = Substitute.For<ISandboxRunner>();
    private readonly ISentinelGateway _sentinel = Substitute.For<ISentinelGateway>();
    private readonly SandboxOrchestrator _sut;

    public SandboxOrchestratorTests()
    {
        _sut = new SandboxOrchestrator(_runner, _sentinel, NullLogger<SandboxOrchestrator>.Instance);
    }

    private static ExecutionRequest MakeRequest(string agentId = "agent-1")
    {
        return new()
        {
            Language = ExecutionLanguage.Python,
            Code = "print('hello')",
            AgentId = agentId,
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    private static ExecutionResult MakeSuccessResult(Guid? auditId = null)
    {
        return new()
        {
            Succeeded = true,
            ExitCode = 0,
            Output = "hello\n",
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = false,
            JobId = Guid.NewGuid(),
            DurationMs = 100,
            SentinelAuditId = auditId ?? Guid.NewGuid()
        };
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDeniedResult_WhenSentinelDenies()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest();
        _sentinel.AuthorizeExecutionAsync(request, ct)
            .Returns(SentinelAuthorizationResult.Deny("blocked by policy", Guid.NewGuid()));

        // Act
        var result = await _sut.ExecuteAsync(request, ct);

        // Assert
        result.PolicyDenied.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
        await _runner.DidNotReceive().RunAsync(Arg.Any<ExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DeniedResult_CarriesReasonAndAuditId()
    {
        // Arrange - the sandbox is stateless, so the caller records denials from the returned result.
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest();
        var auditId = Guid.NewGuid();
        _sentinel.AuthorizeExecutionAsync(request, ct)
            .Returns(SentinelAuthorizationResult.Deny("policy denied", auditId));

        // Act
        var result = await _sut.ExecuteAsync(request, ct);

        // Assert
        result.PolicyDenied.Should().BeTrue();
        result.PolicyDenialReason.Should().Be("policy denied");
        result.SentinelAuditId.Should().Be(auditId);
    }

    [Fact]
    public async Task ExecuteAsync_RunsCode_WhenSentinelAllows()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest();
        var auditId = Guid.NewGuid();
        _sentinel.AuthorizeExecutionAsync(request, ct)
            .Returns(SentinelAuthorizationResult.Allow(auditId));
        _runner.RunAsync(request, ct).Returns(MakeSuccessResult());

        // Act
        var result = await _sut.ExecuteAsync(request, ct);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.PolicyDenied.Should().BeFalse();
        await _runner.Received(1).RunAsync(request, ct);
    }

    [Fact]
    public async Task ExecuteAsync_AttachesSentinelAuditId_ToResult()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest();
        var auditId = Guid.NewGuid();
        _sentinel.AuthorizeExecutionAsync(request, ct)
            .Returns(SentinelAuthorizationResult.Allow(auditId));
        _runner.RunAsync(request, ct).Returns(MakeSuccessResult());

        // Act
        var result = await _sut.ExecuteAsync(request, ct);

        // Assert
        result.SentinelAuditId.Should().Be(auditId);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var act = async () => await _sut.ExecuteAsync(null!, ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
