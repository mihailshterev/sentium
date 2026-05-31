using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Application.Agents;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Infrastructure.Security;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class AgentServiceTests
{
    private readonly IAgentManager _manager = Substitute.For<IAgentManager>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly AgentService _service;

    public AgentServiceTests()
    {
        _currentUser.UserId.Returns(Guid.NewGuid());
        _service = new AgentService(_manager, new PassThroughHybridCache(), _currentUser);
    }

    private static AgentResponse MakeResponse(Guid? id = null, string name = "TestAgent") =>
        new(id ?? Guid.NewGuid(), name, "A test agent", "gemma3:1b",
            DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetAgentsAsync_ReturnsAgentList_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var agents = new List<AgentResponse> { MakeResponse() };
        _manager.GetAgentsAsync(ct).Returns(agents);

        // Act
        var result = await _service.GetAgentsAsync(ct);

        // Assert
        result.Should().BeEquivalentTo(agents);
        await _manager.Received(1).GetAgentsAsync(ct);
    }

    [Fact]
    public async Task GetAgentByIdAsync_ReturnsAgent_WhenIdExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var expected = MakeResponse(id);
        _manager.GetAgentByIdAsync(id, ct).Returns(expected);

        // Act
        var result = await _service.GetAgentByIdAsync(id, ct);

        // Assert
        result.Should().Be(expected);
        await _manager.Received(1).GetAgentByIdAsync(id, ct);
    }

    [Fact]
    public async Task CreateAgentAsync_CreatesAndReturnsAgent_WhenRequestIsValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest("NewAgent", "Desc", "gemma3:1b");
        var expected = MakeResponse(name: "NewAgent");
        _manager.CreateAgentAsync(request, ct).Returns(expected);

        // Act
        var result = await _service.CreateAgentAsync(request, ct);

        // Assert
        result.Should().Be(expected);
        await _manager.Received(1).CreateAgentAsync(request, ct);
    }

    [Fact]
    public async Task UpdateAgentAsync_CallsManager_WhenAgentExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New description");
        _manager.UpdateAgentAsync(id, request, ct).Returns(Task.CompletedTask);

        // Act
        await _service.UpdateAgentAsync(id, request, ct);

        // Assert
        await _manager.Received(1).UpdateAgentAsync(id, request, ct);
    }

    [Fact]
    public async Task DeleteAgentAsync_CallsManager_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _manager.DeleteAgentAsync(id, ct).Returns(Task.CompletedTask);

        // Act
        await _service.DeleteAgentAsync(id, ct);

        // Assert
        await _manager.Received(1).DeleteAgentAsync(id, ct);
    }
}
