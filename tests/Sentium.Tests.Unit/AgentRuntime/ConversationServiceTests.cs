using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Application.Conversations;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class ConversationServiceTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly ConversationService _service;

    public ConversationServiceTests()
    {
        _service = new ConversationService(_repository);
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsConversationSummaries_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var expected = new List<ConversationSummary>
        {
            new(Guid.NewGuid(), "Chat 1", "gemma3:1b", DateTime.UtcNow)
        };
        _repository.GetConversationsAsync(ct).Returns(expected);

        // Act
        var result = await _service.GetConversationsAsync(ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
        await _repository.Received(1).GetConversationsAsync(ct);
    }

    [Fact]
    public async Task GetConversationAsync_ReturnsConversation_WhenIdExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var expected = new ConversationResponse(id, "Chat", "gemma3:1b", DateTime.UtcNow, []);
        _repository.GetConversationAsync(id, ct).Returns(expected);

        // Act
        var result = await _service.GetConversationAsync(id, ct);

        // Assert
        result.Should().Be(expected);
        await _repository.Received(1).GetConversationAsync(id, ct);
    }

    [Fact]
    public async Task CreateConversationAsync_ReturnsSummary_WhenRequestIsValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateConversationRequest("New Chat", "gemma3:1b");
        var expected = new ConversationSummary(Guid.NewGuid(), "New Chat", "gemma3:1b", DateTime.UtcNow);
        _repository.CreateConversationAsync(request, ct).Returns(expected);

        // Act
        var result = await _service.CreateConversationAsync(request, ct);

        // Assert
        result.Should().Be(expected);
        await _repository.Received(1).CreateConversationAsync(request, ct);
    }

    [Fact]
    public async Task DeleteConversationAsync_CallsRepository_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.DeleteConversationAsync(id, ct).Returns(Task.CompletedTask);

        // Act
        await _service.DeleteConversationAsync(id, ct);

        // Assert
        await _repository.Received(1).DeleteConversationAsync(id, ct);
    }

    [Fact]
    public async Task AddMessageAsync_CallsRepository_WhenCalledWithValidData()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var convId = Guid.NewGuid();
        _repository.AddMessageAsync(convId, "user", "Hello", null, null, null, ct).Returns(Task.CompletedTask);

        // Act
        await _service.AddMessageAsync(convId, "user", "Hello", ct: ct);

        // Assert
        await _repository.Received(1).AddMessageAsync(convId, "user", "Hello", null, null, null, ct);
    }
}
