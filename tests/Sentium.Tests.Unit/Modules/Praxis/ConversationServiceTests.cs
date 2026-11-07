using Sentium.Infrastructure.Caching;
using FluentAssertions;
using NSubstitute;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Dtos;
using Sentium.Core.Results;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

public sealed class ConversationServiceTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly ConversationService _service;

    public ConversationServiceTests()
    {
        _service = new ConversationService(_repository, new PassThroughScopedCache());
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsPagedConversationSummaries_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var expected = new List<ConversationSummary>
        {
            new(Guid.NewGuid(), "Chat 1", "gemma3:1b", DateTime.UtcNow)
        };
        _repository.GetConversationsAsync(1, 20, null, ct).Returns((expected, expected.Count));

        // Act
        var result = await _service.GetConversationsAsync(1, 20, null, ct);

        // Assert
        result.Items.Should().BeEquivalentTo(expected);
        result.TotalCount.Should().Be(expected.Count);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        await _repository.Received(1).GetConversationsAsync(1, 20, null, ct);
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
        _repository.DeleteConversationAsync(id, ct).Returns(true);

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

    [Fact]
    public async Task IsTitlePendingAsync_DelegatesToRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.IsTitlePendingAsync(id, ct).Returns(true);

        (await _service.IsTitlePendingAsync(id, ct)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrySetGeneratedTitleAsync_InvalidatesList_OnlyWhenStored(bool stored)
    {
        var ct = TestContext.Current.CancellationToken;
        var cache = Substitute.For<IScopedCache>();
        var service = new ConversationService(_repository, cache);
        var id = Guid.NewGuid();
        _repository.TrySetGeneratedTitleAsync(id, "Trip planning", ct).Returns(stored);

        var result = await service.TrySetGeneratedTitleAsync(id, "Trip planning", ct);

        result.Should().Be(stored);
        await cache.Received(stored ? 1 : 0).InvalidateTagAsync("conversations", ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenameConversationAsync_TrimsTitle_AndInvalidatesList_OnlyWhenRenamed(bool renamed)
    {
        var ct = TestContext.Current.CancellationToken;
        var cache = Substitute.For<IScopedCache>();
        var service = new ConversationService(_repository, cache);
        var id = Guid.NewGuid();
        _repository.RenameConversationAsync(id, "Trip planning", ct).Returns(renamed);

        var result = await service.RenameConversationAsync(id, "  Trip planning  ", ct);

        result.Should().Be(renamed);
        await _repository.Received(1).RenameConversationAsync(id, "Trip planning", ct);
        await cache.Received(renamed ? 1 : 0).InvalidateTagAsync("conversations", ct);
    }

    [Fact]
    public async Task GetConversationsAsync_PassesNormalizedSearch_ToRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.GetConversationsAsync(1, 20, "trip", ct).Returns((new List<ConversationSummary>(), 0));

        await _service.GetConversationsAsync(1, 20, "  trip ", ct);

        await _repository.Received(1).GetConversationsAsync(1, 20, "trip", ct);
    }

    [Fact]
    public async Task GetConversationsAsync_CachesSearches_UnderTheirOwnKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var cache = Substitute.For<IScopedCache>();
        var service = new ConversationService(_repository, cache);

        await service.GetConversationsAsync(1, 20, "Trip", ct);

        await cache.Received(1).GetOrCreateAsync(
            "conversations:page:1:20:q:trip",
            Arg.Any<Func<CancellationToken, ValueTask<PagedResponse<ConversationSummary>>>>(),
            "conversations",
            ct);
    }
}
