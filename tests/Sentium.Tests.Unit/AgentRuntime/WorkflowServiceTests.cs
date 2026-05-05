using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Application.WorkflowManagement;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class WorkflowServiceTests
{
    private readonly IWorkflowManager _manager = Substitute.For<IWorkflowManager>();
    private readonly WorkflowService _service;

    public WorkflowServiceTests()
    {
        _service = new WorkflowService(_manager);
    }

    private static WorkflowResponse MakeResponse(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "WF-1", "Test workflow",
            DateTime.UtcNow, DateTime.UtcNow, []);

    [Fact]
    public async Task GetWorkflowsAsync_DelegatesToManager()
    {
        var expected = new List<WorkflowResponse> { MakeResponse() };
        _manager.GetWorkflowsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _service.GetWorkflowsAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetWorkflowAsync_DelegatesToManager()
    {
        var id = Guid.NewGuid();
        var expected = MakeResponse(id);
        _manager.GetWorkflowAsync(id, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _service.GetWorkflowAsync(id, TestContext.Current.CancellationToken);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task CreateWorkflowAsync_DelegatesToManager()
    {
        var request = new CreateWorkflowRequest("NewWF", "desc", []);
        var expected = MakeResponse();
        _manager.CreateWorkflowAsync(request, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _service.CreateWorkflowAsync(request, TestContext.Current.CancellationToken);

        result.Should().Be(expected);
        await _manager.Received(1).CreateWorkflowAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkflowAsync_DelegatesToManager()
    {
        var id = Guid.NewGuid();
        var request = new UpdateWorkflowRequest("Updated", "new desc", []);
        _manager.UpdateWorkflowAsync(id, request, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.UpdateWorkflowAsync(id, request, TestContext.Current.CancellationToken);

        await _manager.Received(1).UpdateWorkflowAsync(id, request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteWorkflowAsync_DelegatesToManager()
    {
        var id = Guid.NewGuid();
        _manager.DeleteWorkflowAsync(id, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.DeleteWorkflowAsync(id, TestContext.Current.CancellationToken);

        await _manager.Received(1).DeleteWorkflowAsync(id, Arg.Any<CancellationToken>());
    }
}
