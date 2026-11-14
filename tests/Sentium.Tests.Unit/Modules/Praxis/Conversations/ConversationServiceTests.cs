using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Core.Results;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Conversations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Conversations;

public sealed class ConversationServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IAgentRoster _roster = Substitute.For<IAgentRoster>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ConversationService _service;

    public ConversationServiceTests()
    {
        _currentUser.UserId.Returns(UserId);
        _service = CreateService(new PassThroughScopedCache());
    }

    private ConversationService CreateService(IScopedCache cache)
        => new(_repository, cache, _roster, _currentUser, NullLogger<ConversationService>.Instance);

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
        result.Value.Should().Be(expected);
        await _repository.Received(1).CreateConversationAsync(request, ct);
    }

    [Fact]
    public async Task CreateConversationAsync_StoresTheCanonicalAgentName()
    {
        var ct = TestContext.Current.CancellationToken;
        _roster.FindAsync("timelineplanner", UserId, ct).Returns(new AgentRosterEntry("TimelinePlanner", "Plans", AgentKind.BuiltIn, null));
        _repository.CreateConversationAsync(Arg.Any<CreateConversationRequest>(), ct)
            .Returns(new ConversationSummary(Guid.NewGuid(), "Chat", "m", DateTime.UtcNow, "TimelinePlanner"));

        var result = await _service.CreateConversationAsync(new CreateConversationRequest(null, "m", "timelineplanner"), ct);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).CreateConversationAsync(Arg.Is<CreateConversationRequest>(r => r.AgentName == "TimelinePlanner"), ct);
    }

    [Fact]
    public async Task CreateConversationAsync_StoresNoAgent_ForTheGeneralAssistant()
    {
        var ct = TestContext.Current.CancellationToken;
        _roster.FindAsync("GeneralAssistant", UserId, ct).Returns(new AgentRosterEntry(AgentRole.GeneralAssistant, "General", AgentKind.BuiltIn, null));

        await _service.CreateConversationAsync(new CreateConversationRequest(null, "m", "GeneralAssistant"), ct);

        await _repository.Received(1).CreateConversationAsync(Arg.Is<CreateConversationRequest>(r => r.AgentName == null), ct);
    }

    [Fact]
    public async Task CreateConversationAsync_RejectsAnUnknownAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        _roster.FindAsync("Orchestrator", UserId, ct).Returns((AgentRosterEntry?)null);

        var result = await _service.CreateConversationAsync(new CreateConversationRequest(null, "m", "Orchestrator"), ct);

        result.Status.Should().Be(ResultStatus.Invalid);
        await _repository.DidNotReceiveWithAnyArgs().CreateConversationAsync(default!, ct);
    }

    [Fact]
    public async Task ResolveChatAgentAsync_UsesTheConversationsAgent_WhenItStillExists()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.GetAgentNameAsync(id, UserId, ct).Returns("Planner Bot");
        _roster.FindAsync("Planner Bot", UserId, ct).Returns(new AgentRosterEntry("Planner Bot", "Mine", AgentKind.Custom, null));

        (await _service.ResolveChatAgentAsync(id, UserId, ct)).Should().Be("Planner Bot");
    }

    [Fact]
    public async Task ResolveChatAgentAsync_FallsBackToTheGeneralAssistant()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.GetAgentNameAsync(id, UserId, ct).Returns("Deleted Agent");
        _roster.FindAsync("Deleted Agent", UserId, ct).Returns((AgentRosterEntry?)null);

        (await _service.ResolveChatAgentAsync(id, UserId, ct)).Should().Be(AgentRole.GeneralAssistant);
        (await _service.ResolveChatAgentAsync(null, UserId, ct)).Should().Be(AgentRole.GeneralAssistant);
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
        _repository.AddMessageAsync(convId, "user", "Hello", null, null, ct).Returns(Task.CompletedTask);

        // Act
        await _service.AddMessageAsync(convId, "user", "Hello", ct: ct);

        // Assert
        await _repository.Received(1).AddMessageAsync(convId, "user", "Hello", null, null, ct);
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
        var service = CreateService(cache);
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
        var service = CreateService(cache);
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
        var service = CreateService(cache);

        await service.GetConversationsAsync(1, 20, "Trip", ct);

        await cache.Received(1).GetOrCreateAsync(
            "conversations:page:1:20:q:trip",
            Arg.Any<Func<CancellationToken, ValueTask<PagedResponse<ConversationSummary>>>>(),
            "conversations",
            ct);
    }
}
