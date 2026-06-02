using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Sandbox.Application;
using Sentium.Sandbox.Application.Sentinel;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;
using Xunit;

namespace Sentium.Tests.Unit.Sandbox;

public sealed class SandboxOrchestratorTests
{
    private readonly ISandboxRunner _runner = Substitute.For<ISandboxRunner>();
    private readonly ISentinelGateway _sentinel = Substitute.For<ISentinelGateway>();
    private readonly IExecutionLogRepository _executionLog = Substitute.For<IExecutionLogRepository>();
    private readonly SandboxOrchestrator _sut;

    public SandboxOrchestratorTests()
    {
        _sut = new SandboxOrchestrator(_runner, _sentinel, _executionLog, NullLogger<SandboxOrchestrator>.Instance);
    }

    private static ExecutionRequest MakeRequest(string agentId = "agent-1") =>
        new()
        {
            Language = ExecutionLanguage.Python,
            Code = "print('hello')",
            AgentId = agentId,
            CorrelationId = Guid.NewGuid().ToString()
        };

    private static ExecutionResult MakeSuccessResult(Guid? auditId = null) =>
        new()
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
    public async Task ExecuteAsync_PersistsLogEntry_WhenSentinelDenies()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest();
        _sentinel.AuthorizeExecutionAsync(request, ct)
            .Returns(SentinelAuthorizationResult.Deny("policy denied", Guid.NewGuid()));

        // Act
        await _sut.ExecuteAsync(request, ct);

        // Assert
        await _executionLog.Received(1).AddAsync(
            Arg.Is<ExecutionLogEntry>(e => e.PolicyDenied == true && e.AgentId == "agent-1"), ct);
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
    public async Task ExecuteAsync_PersistsExecutionLog_AfterSuccessfulRun()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = MakeRequest();
        _sentinel.AuthorizeExecutionAsync(request, ct)
            .Returns(SentinelAuthorizationResult.Allow(Guid.NewGuid()));
        _runner.RunAsync(request, ct).Returns(MakeSuccessResult());

        // Act
        await _sut.ExecuteAsync(request, ct);

        // Assert
        await _executionLog.Received(1).AddAsync(
            Arg.Is<ExecutionLogEntry>(e => e.AgentId == "agent-1" && e.Succeeded), ct);
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
