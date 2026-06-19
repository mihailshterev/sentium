using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.Shared.Results;
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
    public async Task GetWorkflows_ReturnsOkWithPagedResponse()
    {
        var list = new List<WorkflowResponse> { MakeResponse() };
        var paged = PagedResponse<WorkflowResponse>.Create(list, 1, 1, 20);
        _workflowService.GetWorkflowsPagedAsync(1, 20, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _controller.GetWorkflows(1, 20, TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(paged);
    }

    [Fact]
    public async Task GetWorkflowRuns_ReturnsPagedResponse()
    {
        var empty = (IReadOnlyList<WorkflowRunSummaryResponse>)new List<WorkflowRunSummaryResponse>();
        _runRepository.GetPagedAsync(1, 20, Arg.Any<CancellationToken>()).Returns((empty, 0));

        var result = await _controller.GetWorkflowRuns(1, 20, TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<OkObjectResult>();
        await _runRepository.Received(1).GetPagedAsync(1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWorkflowRuns_PageSizeExceeds100_ClampsTo100()
    {
        var empty = (IReadOnlyList<WorkflowRunSummaryResponse>)new List<WorkflowRunSummaryResponse>();
        _runRepository.GetPagedAsync(1, 100, Arg.Any<CancellationToken>()).Returns((empty, 0));

        await _controller.GetWorkflowRuns(1, 500, TestContext.Current.CancellationToken);

        await _runRepository.Received(1).GetPagedAsync(1, 100, Arg.Any<CancellationToken>());
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
