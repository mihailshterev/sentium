using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Conversations;
using Sentium.Tests.Integration.Common;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// A long conversation's running summary: due once a batch of messages has left the recent window, built pass by pass,
/// and only ever for its owner.
/// </summary>
public sealed class HistorySummaryTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Summary_IsNotDue_UntilABatchHasLeftTheWindow()
    {
        var userId = Guid.NewGuid();
        var conversationId = await CreateConversationWithMessagesAsync(userId, HistoryWindow.RecentMessages + HistoryWindow.SummaryBatchSize - 1);
        var model = Model("summary");

        var passes = await SummarizeAsync(userId, conversationId, model);

        Assert.Equal(0, passes);
        Assert.Empty(model.ReceivedCalls());
    }

    [Fact]
    public async Task Summary_CoversTheMessagesBeforeTheWindow_AndBuildsOnItself()
    {
        var userId = Guid.NewGuid();
        var conversationId = await CreateConversationWithMessagesAsync(userId, 35);
        var model = Model("first summary", "second summary");

        var passes = await SummarizeAsync(userId, conversationId, model);

        Assert.Equal(1, passes);
        var summary = await GetSummaryAsync(userId, conversationId);
        Assert.Equal("first summary", summary!.Text);

        var messages = await RecentAsync(userId, conversationId, 100);
        Assert.Equal(messages[14].Timestamp, summary.Through);

        await AddMessagesAsync(userId, conversationId, 35, HistoryWindow.SummaryBatchSize);
        Assert.Equal(1, await SummarizeAsync(userId, conversationId, model));

        var secondRequest = model.ReceivedCalls().Last().GetArguments()[0] as IEnumerable<ChatMessage>;
        Assert.Contains("first summary", secondRequest!.Last().Text, StringComparison.Ordinal);
        Assert.Equal("second summary", (await GetSummaryAsync(userId, conversationId))!.Text);
    }

    [Fact]
    public async Task Summary_IsNeverReadOrWrittenForAnotherUser()
    {
        var owner = Guid.NewGuid();
        var conversationId = await CreateConversationWithMessagesAsync(owner, 35);

        Assert.Equal(0, await SummarizeAsync(Guid.NewGuid(), conversationId, Model("stolen")));
        Assert.Null(await GetSummaryAsync(owner, conversationId));
    }

    private static IChatClient Model(params string[] replies)
    {
        var model = Substitute.For<IChatClient>();
        var responses = replies.Select(r => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, r)))).ToArray();
        model.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(responses[0], responses[1..]);
        return model;
    }

    private async Task<int> SummarizeAsync(Guid userId, Guid conversationId, IChatClient model)
    {
        await using var scope = ActingAs(userId);
        var summarizer = new HistorySummarizer(
            scope.ServiceProvider.GetRequiredService<IConversationRepository>(),
            new HistorySummaryGenerator(model, NullLogger<HistorySummaryGenerator>.Instance));

        return await summarizer.SummarizeIfDueAsync(conversationId, userId, Ct);
    }

    private async Task<HistorySummary?> GetSummaryAsync(Guid userId, Guid conversationId)
    {
        await using var scope = ActingAs(userId);
        return await scope.ServiceProvider.GetRequiredService<IConversationRepository>().GetHistorySummaryAsync(conversationId, userId, Ct);
    }

    private async Task<IReadOnlyList<MessageResponse>> RecentAsync(Guid userId, Guid conversationId, int limit)
    {
        await using var scope = ActingAs(userId);
        return await scope.ServiceProvider.GetRequiredService<IConversationRepository>().GetRecentMessagesAsync(conversationId, userId, limit, Ct);
    }

    private async Task<Guid> CreateConversationWithMessagesAsync(Guid userId, int count)
    {
        Guid conversationId;
        await using (var scope = ActingAs(userId))
        {
            var created = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
                .CreateConversationAsync(new CreateConversationRequest("Long", "m"), Ct);
            conversationId = created.Id;
        }

        await AddMessagesAsync(userId, conversationId, 0, count);
        return conversationId;
    }

    private async Task AddMessagesAsync(Guid userId, Guid conversationId, int first, int count)
    {
        await using var scope = ActingAs(userId);
        var repository = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        for (var i = first; i < first + count; i++)
        {
            await repository.AddMessageAsync(conversationId, i % 2 == 0 ? "User" : "Assistant", $"message {i}", ct: Ct);
        }
    }

    private AsyncServiceScope ActingAs(Guid userId)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().ActAs(userId);
        return scope;
    }
}
