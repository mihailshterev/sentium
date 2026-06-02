using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class WorkflowsControllerTests
{
    private readonly IWorkflowService _workflowService = Substitute.For<IWorkflowService>();
    private readonly IWorkflowRunRepository _runRepository = Substitute.For<IWorkflowRunRepository>();
    private readonly WorkflowsController _controller;

    public WorkflowsControllerTests()
    {
        _controller = new WorkflowsController(_workflowService, _runRepository);
    }

    private static WorkflowResponse MakeResponse(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), "WF", "desc", DateTime.UtcNow, DateTime.UtcNow, []);

    [Fact]
    public async Task GetWorkflows_ReturnsOkWithList()
    {
        var list = new List<WorkflowResponse> { MakeResponse() };
        _workflowService.GetWorkflowsAsync(Arg.Any<CancellationToken>()).Returns(list);

        var result = await _controller.GetWorkflows(TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(list);
    }

    [Fact]
    public async Task GetWorkflowRuns_DefaultCount_ReturnsCapped()
    {
        _runRepository.GetRecentAsync(20, Arg.Any<CancellationToken>()).Returns([]);

        await _controller.GetWorkflowRuns(20, TestContext.Current.CancellationToken);

        await _runRepository.Received(1).GetRecentAsync(20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWorkflowRuns_CountExceeds100_CapAt100()
    {
        _runRepository.GetRecentAsync(100, Arg.Any<CancellationToken>()).Returns([]);

        await _controller.GetWorkflowRuns(500, TestContext.Current.CancellationToken);

        await _runRepository.Received(1).GetRecentAsync(100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkflow_ValidRequest_ReturnsCreated()
    {
        var request = new CreateWorkflowRequest("WF", "desc", []);
        var created = MakeResponse();
        _workflowService.CreateWorkflowAsync(request, Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.CreateWorkflow(request, TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().Be(created);
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _workflowService.DeleteWorkflowAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.DeleteWorkflow(id, TestContext.Current.CancellationToken);

        result.Should().BeOfType<NoContentResult>();
    }
}
