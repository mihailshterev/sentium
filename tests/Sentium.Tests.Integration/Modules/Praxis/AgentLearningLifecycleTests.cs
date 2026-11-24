using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Learnings;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Learnings over months of use: restated ones are not stored twice, recalls are counted, failed embeddings are
/// retried, and (when configured) learnings nobody recalls any more are retired.
/// </summary>
public sealed class AgentLearningLifecycleTests(WordEmbeddingWebApplicationFactory factory) : IClassFixture<WordEmbeddingWebApplicationFactory>
{
    private const string Agent = "ResearchAnalyst";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RestatedPrivateLearning_IsNotStoredTwice()
    {
        var userId = Guid.NewGuid();
        await using var scope = ActingAs(userId);
        var learnings = scope.ServiceProvider.GetRequiredService<IAgentLearningService>();

        var first = await learnings.CaptureAsync(Capture(userId, "Use pnpm instead of npm for the portal build"), Ct);
        var again = await learnings.CaptureAsync(Capture(userId, "use PNPM instead of npm for the portal build."), Ct);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await CountAsync(userId));
    }

    [Fact]
    public async Task Recall_IsCounted()
    {
        var userId = Guid.NewGuid();
        await using var scope = ActingAs(userId);
        var learnings = scope.ServiceProvider.GetRequiredService<IAgentLearningService>();
        var captured = await learnings.CaptureAsync(Capture(userId, "Timeline exports need the ISO week number column"), Ct);

        var recalled = await learnings.RecallRelevantAsync("timeline exports need the ISO week number", userId, ct: Ct);

        Assert.Single(recalled);
        var row = await FindAsync(captured.Id);
        Assert.Equal(1, row!.RecallCount);
        Assert.NotNull(row.LastRecalledAt);
    }

    [Fact]
    public async Task FailedEmbedding_IsRetried_AndThenRecalled()
    {
        var userId = Guid.NewGuid();
        var pending = new AgentLearning
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgentName = Agent,
            Content = "Backups run at three in the morning local time",
            Tags = string.Empty,
            CapturedAt = DateTimeOffset.UtcNow,
            IsIngested = false
        };
        await AddAsync(pending);

        await using (var system = SystemScope())
        {
            Assert.True(await system.ServiceProvider.GetRequiredService<IAgentLearningService>().RetryPendingIngestionAsync(1000, Ct) >= 1);
        }

        Assert.True((await FindAsync(pending.Id))!.IsIngested);
        await using var scope = ActingAs(userId);
        var recalled = await scope.ServiceProvider.GetRequiredService<IAgentLearningService>()
            .RecallRelevantAsync("backups run at three in the morning", userId, ct: Ct);
        Assert.Single(recalled);
    }

    [Fact]
    public async Task UnusedLearnings_AreRetired_OnlyWhenConfigured()
    {
        var userId = Guid.NewGuid();
        Guid staleId;
        await using (var scope = ActingAs(userId))
        {
            staleId = (await scope.ServiceProvider.GetRequiredService<IAgentLearningService>()
                .CaptureAsync(Capture(userId, "The old router password rotates monthly"), Ct)).Id;
        }

        await SetCapturedAtAsync(staleId, DateTimeOffset.UtcNow.AddDays(-400));

        Assert.Equal(0, await SweepAsync(retentionDays: null));
        Assert.NotNull(await FindAsync(staleId));

        Assert.True(await SweepAsync(retentionDays: 365) >= 1);
        Assert.Null(await FindAsync(staleId));
    }

    private static CaptureAgentLearningRequest Capture(Guid userId, string content) => new(Agent, content, UserId: userId);

    private async Task<int> SweepAsync(int? retentionDays)
    {
        await using var scope = SystemScope();
        var settings = new Dictionary<string, string?>();
        if (retentionDays is { } days)
        {
            settings["Praxis:UnusedLearningRetentionDays"] = days.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var sweep = new UnusedLearningRetention(
            scope.ServiceProvider.GetRequiredService<IAgentLearningRepository>(),
            scope.ServiceProvider.GetRequiredService<IAgentLearningService>(),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<UnusedLearningRetention>.Instance);

        return await sweep.SweepAsync(DateTimeOffset.UtcNow, Ct);
    }

    private async Task<int> CountAsync(Guid userId)
    {
        await using var scope = SystemScope();
        return await scope.ServiceProvider.GetRequiredService<PraxisDbContext>().AgentLearnings.CountAsync(l => l.UserId == userId, Ct);
    }

    private async Task<AgentLearning?> FindAsync(Guid id)
    {
        await using var scope = SystemScope();
        return await scope.ServiceProvider.GetRequiredService<PraxisDbContext>().AgentLearnings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, Ct);
    }

    private async Task AddAsync(AgentLearning learning)
    {
        await using var scope = SystemScope();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        db.AgentLearnings.Add(learning);
        await db.SaveChangesAsync(Ct);
    }

    private async Task SetCapturedAtAsync(Guid id, DateTimeOffset capturedAt)
    {
        await using var scope = SystemScope();
        await scope.ServiceProvider.GetRequiredService<PraxisDbContext>().AgentLearnings
            .Where(l => l.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.CapturedAt, capturedAt), Ct);
    }

    private AsyncServiceScope ActingAs(Guid userId)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().ActAs(userId);
        return scope;
    }

    private AsyncServiceScope SystemScope()
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return scope;
    }
}
