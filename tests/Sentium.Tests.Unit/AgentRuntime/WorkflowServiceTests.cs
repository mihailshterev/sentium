using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Application.WorkflowManagement;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class WorkflowServiceTests
{
    private readonly IWorkflowRepository _repository = Substitute.For<IWorkflowRepository>();
    private readonly WorkflowService _service;

    public WorkflowServiceTests()
    {
        _service = new WorkflowService(_repository, new PassThroughScopedCache());
    }

    private static WorkflowResponse MakeResponse(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), "WF-1", "Test workflow",
            DateTime.UtcNow, DateTime.UtcNow, []);

    [Fact]
    public async Task GetWorkflowsAsync_DelegatesToManager()
    {
        var expected = new List<WorkflowResponse> { MakeResponse() };
        _repository.GetWorkflowsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _service.GetWorkflowsAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetWorkflowAsync_DelegatesToManager()
    {
        var id = Guid.NewGuid();
        var expected = MakeResponse(id);
        _repository.GetWorkflowAsync(id, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _service.GetWorkflowAsync(id, TestContext.Current.CancellationToken);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task CreateWorkflowAsync_DelegatesToManager()
    {
        var request = new CreateWorkflowRequest("NewWF", "desc", []);
        var expected = MakeResponse();
        _repository.CreateWorkflowAsync(request, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _service.CreateWorkflowAsync(request, TestContext.Current.CancellationToken);

        result.Should().Be(expected);
        await _repository.Received(1).CreateWorkflowAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkflowAsync_DelegatesToManager()
    {
        var id = Guid.NewGuid();
        var request = new UpdateWorkflowRequest("Updated", "new desc", []);
        _repository.UpdateWorkflowAsync(id, request, Arg.Any<CancellationToken>()).Returns(true);

        await _service.UpdateWorkflowAsync(id, request, TestContext.Current.CancellationToken);

        await _repository.Received(1).UpdateWorkflowAsync(id, request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteWorkflowAsync_DelegatesToManager()
    {
        var id = Guid.NewGuid();
        _repository.DeleteWorkflowAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        await _service.DeleteWorkflowAsync(id, TestContext.Current.CancellationToken);

        await _repository.Received(1).DeleteWorkflowAsync(id, Arg.Any<CancellationToken>());
    }
}
