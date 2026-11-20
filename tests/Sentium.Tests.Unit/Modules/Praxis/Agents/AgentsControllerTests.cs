using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Agents;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class AgentsControllerTests
{
    private readonly IAgentService _agentService = Substitute.For<IAgentService>();
    private readonly AgentsController _controller;

    public AgentsControllerTests()
    {
        _controller = new AgentsController(_agentService);
    }

    private static AgentResponse MakeResponse(Guid? id = null, string name = "Agent") =>
        new(id ?? Guid.NewGuid(), name, "Desc", "gemma3:1b", DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetAgents_ReturnsOkWithPagedResponse_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var agents = new List<AgentResponse> { MakeResponse() };
        var paged = PagedResponse<AgentResponse>.Create(agents, 1, 1, 20);
        _agentService.GetAgentsPagedAsync(1, 20, ct).Returns(paged);

        // Act
        var result = await _controller.GetAgents(1, 20, ct);

        // Assert
        Assert.Equal(paged, Assert.IsType<OkObjectResult>(result.Result).Value);
        await _agentService.Received(1).GetAgentsPagedAsync(1, 20, ct);
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
        Assert.Equal(agent, Assert.IsType<OkObjectResult>(result.Result).Value);
        await _agentService.Received(1).GetAgentByIdAsync(id, ct);
    }

    [Fact]
    public async Task CreateAgent_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest("MyAgent", "Desc", "gemma3:1b");
        var created = MakeResponse(name: "MyAgent");
        _agentService.CreateAgentAsync(request, ct).Returns(Result<AgentResponse>.Success(created));

        // Act
        var result = await _controller.CreateAgent(request, ct);

        // Assert
        Assert.Equal(created, Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        await _agentService.Received(1).CreateAgentAsync(request, ct);
    }

    [Fact]
    public async Task CreateAgent_ReturnsConflict_WhenNameExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest("Dupe", "Desc", "gemma3:1b");
        _agentService.CreateAgentAsync(request, ct)
            .Returns(Result<AgentResponse>.Conflict("An agent named 'Dupe' already exists."));

        // Act
        var result = await _controller.CreateAgent(request, ct);

        // Assert
        Assert.Contains("Dupe", Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(result.Result).Value).Detail, StringComparison.Ordinal);
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
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateAgent_ReturnsNotFound_WhenAgentMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New desc");
        _agentService.UpdateAgentAsync(id, request, ct).Returns(Result<AgentResponse>.NotFound());

        // Act
        var result = await _controller.UpdateAgent(id, request, ct);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateAgent_ReturnsConflict_WhenNameTaken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Taken", "New desc");
        _agentService.UpdateAgentAsync(id, request, ct)
            .Returns(Result<AgentResponse>.Conflict("An agent named 'Taken' already exists."));

        // Act
        var result = await _controller.UpdateAgent(id, request, ct);

        // Assert
        Assert.Contains("Taken", Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(result).Value).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAgent_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New desc");
        _agentService.UpdateAgentAsync(id, request, ct).Returns(Result<AgentResponse>.Success(MakeResponse(id)));

        // Act
        var result = await _controller.UpdateAgent(id, request, ct);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _agentService.Received(1).UpdateAgentAsync(id, request, ct);
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
        Assert.IsType<NotFoundResult>(result);
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
        Assert.IsType<NoContentResult>(result);
        await _agentService.Received(1).DeleteAgentAsync(id, ct);
    }
}
