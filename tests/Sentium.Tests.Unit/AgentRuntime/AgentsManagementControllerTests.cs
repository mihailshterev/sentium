using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class AgentsManagementControllerTests
{
    private readonly IAgentService _agentService = Substitute.For<IAgentService>();
    private readonly AgentsManagementController _controller;

    public AgentsManagementControllerTests()
    {
        _controller = new AgentsManagementController(_agentService);
    }

    private static AgentResponse MakeResponse(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Agent", "Desc", "gemma3:1b", DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetAgents_ReturnsOkWithList_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var agents = new List<AgentResponse> { MakeResponse() };
        _agentService.GetAgentsAsync(ct).Returns(agents);

        // Act
        var result = await _controller.GetAgents(ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(agents);
        await _agentService.Received(1).GetAgentsAsync(ct);
    }

    [Fact]
    public async Task GetAgentById_ReturnsOk_WhenAgentExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var agent = MakeResponse(id);
        _agentService.GetAgentByIdAsync(id, ct).Returns(agent);

        // Act
        var result = await _controller.GetAgentById(id, ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(agent);
        await _agentService.Received(1).GetAgentByIdAsync(id, ct);
    }

    [Fact]
    public async Task CreateAgent_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest("MyAgent", "Desc", "gemma3:1b");
        var created = new AgentResponse(Guid.NewGuid(), "MyAgent", "Desc", "gemma3:1b", DateTime.UtcNow, DateTime.UtcNow);
        _agentService.CreateAgentAsync(request, ct).Returns(created);

        // Act
        var result = await _controller.CreateAgent(request, ct);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().Be(created);
        await _agentService.Received(1).CreateAgentAsync(request, ct);
    }

    [Fact]
    public async Task GetAgentById_ReturnsNotFound_WhenAgentMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _agentService.GetAgentByIdAsync(id, ct).Returns((AgentResponse?)null);

        // Act
        var result = await _controller.GetAgentById(id, ct);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateAgent_ReturnsNotFound_WhenAgentMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New desc");
        _agentService.UpdateAgentAsync(id, request, ct).Returns(false);

        // Act
        var result = await _controller.UpdateAgent(id, request, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteAgent_ReturnsNotFound_WhenAgentMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _agentService.DeleteAgentAsync(id, ct).Returns(false);

        // Act
        var result = await _controller.DeleteAgent(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateAgent_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New desc");
        _agentService.UpdateAgentAsync(id, request, ct).Returns(true);

        // Act
        var result = await _controller.UpdateAgent(id, request, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _agentService.Received(1).UpdateAgentAsync(id, request, ct);
    }

    [Fact]
    public async Task DeleteAgent_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _agentService.DeleteAgentAsync(id, ct).Returns(true);

        // Act
        var result = await _controller.DeleteAgent(id, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _agentService.Received(1).DeleteAgentAsync(id, ct);
    }
}
