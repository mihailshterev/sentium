using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Workflows;

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

        Assert.Equal(paged, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task GetWorkflowRuns_ReturnsPagedResponse()
    {
        var empty = (IReadOnlyList<WorkflowRunSummaryResponse>)new List<WorkflowRunSummaryResponse>();
        _runRepository.GetPagedAsync(1, 20, null, Arg.Any<CancellationToken>()).Returns((empty, 0));

        var result = await _controller.GetWorkflowRuns(1, 20, null, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        await _runRepository.Received(1).GetPagedAsync(1, 20, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWorkflowRuns_PageSizeExceeds100_ClampsTo100()
    {
        var empty = (IReadOnlyList<WorkflowRunSummaryResponse>)new List<WorkflowRunSummaryResponse>();
        _runRepository.GetPagedAsync(1, 100, null, Arg.Any<CancellationToken>()).Returns((empty, 0));

        await _controller.GetWorkflowRuns(1, 500, null, TestContext.Current.CancellationToken);

        await _runRepository.Received(1).GetPagedAsync(1, 100, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkflow_ValidRequest_ReturnsCreated()
    {
        var request = new CreateWorkflowRequest("WF", "desc", []);
        var created = MakeResponse();
        _workflowService.CreateWorkflowAsync(request, Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.CreateWorkflow(request, TestContext.Current.CancellationToken);

        Assert.Equal(created, Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _workflowService.DeleteWorkflowAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.DeleteWorkflow(id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenameWorkflowRun_TrimsTitle_AndReturnsNoContentOrNotFound(bool renamed)
    {
        var runId = Guid.NewGuid();
        _runRepository.RenameAsync(runId, "Nightly report", Arg.Any<CancellationToken>()).Returns(renamed);

        var result = await _controller.RenameWorkflowRun(runId, new UpdateTitleRequest(" Nightly report "), TestContext.Current.CancellationToken);

        if (renamed)
        {
            Assert.IsType<NoContentResult>(result);
        }
        else
        {
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
