using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class AgentServiceTests
{
    private readonly IAgentRepository _repository = Substitute.For<IAgentRepository>();
    private readonly IAgentRegistry _registry = Substitute.For<IAgentRegistry>();
    private readonly AgentService _service;

    public AgentServiceTests()
    {
        _registry.GetRegisteredNames().Returns(new[] { "Validator", "Orchestrator", "Summarizer", "GeneralAssistant" });
        _service = new AgentService(_repository, new PassThroughScopedCache(), _registry, new RunTargetNotifier([], NullLogger<RunTargetNotifier>.Instance));
    }

    private static AgentResponse MakeResponse(Guid? id = null, string name = "TestAgent")
    {
        return new(id ?? Guid.NewGuid(), name, "A test agent", "gemma3:1b",
            DateTime.UtcNow, DateTime.UtcNow);
    }

    [Fact]
    public async Task GetAgentsAsync_ReturnsAgentList_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var agents = new List<AgentResponse> { MakeResponse() };
        _repository.GetAgentsAsync(ct).Returns(agents);

        // Act
        var result = await _service.GetAgentsAsync(ct);

        // Assert
        Assert.Equivalent(agents, result);
        await _repository.Received(1).GetAgentsAsync(ct);
    }

    [Fact]
    public async Task GetAgentByIdAsync_ReturnsAgent_WhenIdExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var expected = MakeResponse(id);
        _repository.GetAgentByIdAsync(id, ct).Returns(expected);

        // Act
        var result = await _service.GetAgentByIdAsync(id, ct);

        // Assert
        Assert.Equal(expected, result);
        await _repository.Received(1).GetAgentByIdAsync(id, ct);
    }

    [Fact]
    public async Task CreateAgentAsync_CreatesAndReturnsAgent_WhenNameIsUnique()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest("NewAgent", "Desc", "gemma3:1b");
        var expected = MakeResponse(name: "NewAgent");
        _repository.NameExistsAsync(request.Name, ct: ct).Returns(false);
        _repository.CreateAgentAsync(request, ct).Returns(expected);

        // Act
        var result = await _service.CreateAgentAsync(request, ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        await _repository.Received(1).CreateAgentAsync(request, ct);
    }

    [Fact]
    public async Task CreateAgentAsync_ReturnsConflict_WhenNameExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest("Existing", "Desc", "gemma3:1b");
        _repository.NameExistsAsync(request.Name, ct: ct).Returns(true);

        // Act
        var result = await _service.CreateAgentAsync(request, ct);

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Existing", result.Error, StringComparison.Ordinal);
        await _repository.DidNotReceive().CreateAgentAsync(Arg.Any<CreateAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Validator")]
    [InlineData("validator")] // case-insensitive: native agents take precedence in the factory regardless of casing
    [InlineData("Summarizer")]
    public async Task CreateAgentAsync_ReturnsConflict_WhenNameIsReservedBuiltIn(string reserved)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentRequest(reserved, "Desc", "gemma3:1b");

        // Act
        var result = await _service.CreateAgentAsync(request, ct);

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("reserved", result.Error, StringComparison.Ordinal);
        await _repository.DidNotReceive().NameExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CreateAgentAsync(Arg.Any<CreateAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAgentAsync_ReturnsConflict_WhenRenamedToReservedBuiltIn()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Validator", "New description");

        // Act
        var result = await _service.UpdateAgentAsync(id, request, ct);

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("reserved", result.Error, StringComparison.Ordinal);
        await _repository.DidNotReceive().UpdateAgentAsync(Arg.Any<Guid>(), Arg.Any<UpdateAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAgentAsync_ReturnsSuccess_WhenNameIsUnique()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New description");
        _repository.NameExistsAsync(request.Name, excludeId: id, ct: ct).Returns(false);
        _repository.UpdateAgentAsync(id, request, ct).Returns(true);
        _repository.GetAgentByIdAsync(id, ct).Returns(MakeResponse(id, "Updated"));

        // Act
        var result = await _service.UpdateAgentAsync(id, request, ct);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).UpdateAgentAsync(id, request, ct);
    }

    [Fact]
    public async Task UpdateAgentAsync_ReturnsConflict_WhenNameTakenByAnother()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Taken", "New description");
        _repository.NameExistsAsync(request.Name, excludeId: id, ct: ct).Returns(true);

        // Act
        var result = await _service.UpdateAgentAsync(id, request, ct);

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Taken", result.Error, StringComparison.Ordinal);
        await _repository.DidNotReceive().UpdateAgentAsync(Arg.Any<Guid>(), Arg.Any<UpdateAgentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAgentAsync_ReturnsNotFound_WhenAgentMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentRequest(id, "Updated", "New description");
        _repository.NameExistsAsync(request.Name, excludeId: id, ct: ct).Returns(false);
        _repository.UpdateAgentAsync(id, request, ct).Returns(false);

        // Act
        var result = await _service.UpdateAgentAsync(id, request, ct);

        // Assert
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteAgentAsync_CallsManager_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.DeleteAgentAsync(id, ct).Returns(true);

        // Act
        await _service.DeleteAgentAsync(id, ct);

        // Assert
        await _repository.Received(1).DeleteAgentAsync(id, ct);
    }
}
