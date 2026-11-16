using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Controllers;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

public sealed class SandboxControllerTests
{
    private readonly ISandboxRunner _runner = Substitute.For<ISandboxRunner>();
    private readonly SandboxController _controller;

    public SandboxControllerTests()
    {
        _controller = new SandboxController(_runner)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTheRunnerResult()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var jobId = Guid.NewGuid();
        _runner.RunAsync(Arg.Any<ExecutionRequest>(), ct).Returns(new ExecutionResult
        {
            Succeeded = true,
            ExitCode = 0,
            Output = "hello\n",
            Error = string.Empty,
            TimedOut = false,
            JobId = jobId,
            DurationMs = 150
        });

        var body = new SandboxExecutionRequest
        {
            Language = "python",
            Code = "print('hello')",
            AgentId = "agent-1"
        };

        // Act
        var result = await _controller.ExecuteAsync(body, ct);

        // Assert
        var response = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SandboxExecutionResponse>().Subject;
        response.Succeeded.Should().BeTrue();
        response.Output.Should().Be("hello\n");
        response.JobId.Should().Be(jobId);
        await _runner.Received(1).RunAsync(
            Arg.Is<ExecutionRequest>(r => r.Language == ExecutionLanguage.Python && r.AgentId == "agent-1"),
            ct);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTheCallersCorrelationId()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _controller.HttpContext.Request.Headers["X-Correlation-ID"] = "corr-1";
        _runner.RunAsync(Arg.Any<ExecutionRequest>(), ct).Returns(new ExecutionResult
        {
            Succeeded = true,
            ExitCode = 0,
            Output = string.Empty,
            Error = string.Empty,
            TimedOut = false,
            JobId = Guid.NewGuid(),
            DurationMs = 1
        });

        // Act
        await _controller.ExecuteAsync(new SandboxExecutionRequest { Language = "Node", Code = "1", AgentId = "agent-1" }, ct);

        // Assert
        await _runner.Received(1).RunAsync(Arg.Is<ExecutionRequest>(r => r.CorrelationId == "corr-1"), ct);
    }
}
