using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Learnings;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class AgentLearningsControllerTests
{
    private readonly IAgentLearningService _learningService = Substitute.For<IAgentLearningService>();
    private readonly AgentLearningsController _controller;

    public AgentLearningsControllerTests()
    {
        _controller = new AgentLearningsController(_learningService);
    }

    private static AgentLearningResponse MakeResponse(Guid? id = null) => new(id ?? Guid.NewGuid(), "TestAgent", "Learning content", "tag1", null, DateTimeOffset.UtcNow, true, false);

    [Fact]
    public async Task GetLearnings_ReturnsOk_WithList()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var learnings = new List<AgentLearningResponse> { MakeResponse() };
        _learningService.GetLearningsAsync(null, 50, ct).Returns(learnings);

        // Act
        var result = await _controller.GetLearnings(null, 50, ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(learnings);
    }

    [Fact]
    public async Task GetStats_ReturnsOk_WithStats()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stats = new AgentLearningStats(10, 3, 7, new Dictionary<string, int> { ["agent1"] = 5 });
        _learningService.GetStatsAsync(ct).Returns(stats);

        // Act
        var result = await _controller.GetStats(ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(stats);
    }

    [Fact]
    public async Task DeleteLearning_ReturnsNoContent_WhenDeleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _learningService.DeleteAsync(id, ct).Returns(true);

        // Act
        var result = await _controller.DeleteLearning(id, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteLearning_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _learningService.DeleteAsync(id, ct).Returns(false);

        // Act
        var result = await _controller.DeleteLearning(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateLearning_ReturnsOk_WhenUpdated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var updated = MakeResponse(id);
        var request = new UpdateAgentLearningRequest("Updated content", "tag1");
        _learningService.UpdateAsync(id, request, ct).Returns(updated);

        // Act
        var result = await _controller.UpdateLearning(id, request, ct);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(updated);
    }

    [Fact]
    public async Task UpdateLearning_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateAgentLearningRequest("Updated content", "tag1");
        _learningService.UpdateAsync(id, request, ct).Returns((AgentLearningResponse?)null);

        // Act
        var result = await _controller.UpdateLearning(id, request, ct);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
