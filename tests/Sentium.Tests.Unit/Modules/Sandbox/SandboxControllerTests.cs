using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Controllers;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Models;
using Sentium.Sandbox.Sentinel;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

public sealed class SandboxControllerTests
{
    private readonly ISandboxRunner _runner = Substitute.For<ISandboxRunner>();
    private readonly ISentinelGateway _sentinel = Substitute.For<ISentinelGateway>();
    private readonly SandboxController _controller;

    public SandboxControllerTests()
    {
        var orchestrator = new SandboxOrchestrator(_runner, _sentinel, NullLogger<SandboxOrchestrator>.Instance);

        _controller = new SandboxController(orchestrator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDeniedResult_WhenPolicyDenied()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _sentinel.AuthorizeExecutionAsync(Arg.Any<ExecutionRequest>(), ct)
            .Returns(SentinelAuthorizationResult.Deny("blocked", Guid.NewGuid()));

        var body = new SandboxExecutionRequest
        {
            Language = "Python",
            Code = "print('hi')",
            AgentId = "agent-1"
        };

        // Act
        var result = await _controller.ExecuteAsync(body, ct);

        // Assert - a denial is a result (200 + PolicyDenied), so Praxis can record it.
        var response = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SandboxExecutionResponse>().Subject;
        response.PolicyDenied.Should().BeTrue();
        response.PolicyDenialReason.Should().Be("blocked");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOk_WhenExecutionSucceeds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var auditId = Guid.NewGuid();
        var successResult = new ExecutionResult
        {
            Succeeded = true,
            ExitCode = 0,
            Output = "hello\n",
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = false,
            JobId = Guid.NewGuid(),
            DurationMs = 150
        };

        _sentinel.AuthorizeExecutionAsync(Arg.Any<ExecutionRequest>(), ct)
            .Returns(SentinelAuthorizationResult.Allow(auditId));
        _runner.RunAsync(Arg.Any<ExecutionRequest>(), ct).Returns(successResult);

        var body = new SandboxExecutionRequest
        {
            Language = "Python",
            Code = "print('hello')",
            AgentId = "agent-1"
        };

        // Act
        var result = await _controller.ExecuteAsync(body, ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
