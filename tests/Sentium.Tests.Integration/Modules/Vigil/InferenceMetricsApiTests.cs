using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Inference;
using Sentium.Infrastructure.Security;
using Sentium.Tests.Integration.Common;
using Sentium.Vigil.Data;
using Sentium.Vigil.Entities;
using Sentium.Vigil.Features.Inference;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Vigil;

/// <summary>
/// Stored inference minutes read back through <c>/api/vigil/inference</c>. Every test writes its own model names,
/// so tests sharing the database never see each other's rows.
/// </summary>
public sealed class InferenceMetricsApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Endpoint = "/api/vigil/inference";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueModel() => $"model-{Guid.NewGuid():N}";

    private async Task SeedAsync(Action<VigilDbContext> seed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VigilDbContext>();
        seed(db);
        await db.SaveChangesAsync(Ct);
    }

    private static InferenceServerMinute ServerMinute(string model, long epochMinute, long generated, double generationSeconds, int deferred = 0) => new()
    {
        EpochMinute = epochMinute,
        Server = InferenceEndpoint.Chat,
        Model = model,
        PromptTokens = 1_000,
        PromptSeconds = 0.5,
        GeneratedTokens = generated,
        GenerationSeconds = generationSeconds,
        PeakRequestsProcessing = 1,
        PeakRequestsDeferred = deferred,
        Samples = 4,
        BusySamples = 2,
    };

    [Fact]
    public async Task Overview_SumsTheMinutesOfABucket_IntoRatesAndPeaks()
    {
        var model = UniqueModel();
        var now = EpochMinutes.From(DateTimeOffset.UtcNow);

        // The 1h range uses one-minute buckets, so the two rows written for the same minute share one bucket.
        await SeedAsync(db => db.InferenceServerMinutes.AddRange(
            ServerMinute(model, now - 5, generated: 100, generationSeconds: 4),
            ServerMinute(model, now - 5, generated: 200, generationSeconds: 8, deferred: 2),
            ServerMinute(model, now - 2, generated: 0, generationSeconds: 0)));

        var overview = await factory.CreateClient().GetFromJsonAsync<InferenceOverviewDto>($"{Endpoint}?range=1h", Ct);

        Assert.Equal("1h", overview!.Range);
        var series = Assert.Single(overview.Series, s => s.Model == model);
        Assert.Equal("Chat", series.Server);
        Assert.Equal(2, series.Points.Count);

        var busy = series.Points[0];
        Assert.Equal(EpochMinutes.ToTime(now - 5), busy.At);
        Assert.Equal(25, busy.GenerationTokensPerSecond);
        Assert.Equal(2_000, busy.PromptTokensPerSecond);
        Assert.Equal(300, busy.GeneratedTokens);
        Assert.Equal(2, busy.PeakRequestsDeferred);
        Assert.Equal(50, busy.BusyPercent);

        Assert.Null(series.Points[1].GenerationTokensPerSecond);
    }

    [Fact]
    public async Task Overview_TotalsUsagePerCaller_AndListsRecentModelLoads()
    {
        var model = UniqueModel();
        var now = EpochMinutes.From(DateTimeOffset.UtcNow);

        await SeedAsync(db =>
        {
            db.InferenceUsageMinutes.AddRange(
                new InferenceUsageMinute { EpochMinute = now - 3, Caller = "Agents", Model = model, Operation = "chat", Requests = 2, InputTokens = 500, OutputTokens = 80, DurationSeconds = 6, FirstChunkSeconds = 1, FirstChunkCount = 2 },
                new InferenceUsageMinute { EpochMinute = now - 1, Caller = "Agents", Model = model, Operation = "chat", Requests = 2, Errors = 1, InputTokens = 100, OutputTokens = 20, DurationSeconds = 2 });
            db.ModelLoadEvents.Add(new ModelLoadEvent
            {
                Id = Guid.CreateVersion7(),
                Server = InferenceEndpoint.Chat,
                Model = model,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10).AddSeconds(45),
                Succeeded = true,
            });
        });

        var overview = await factory.CreateClient().GetFromJsonAsync<InferenceOverviewDto>(Endpoint, Ct);

        Assert.Equal("24h", overview!.Range);
        var usage = Assert.Single(overview.Usage, u => u.Model == model);
        Assert.Equal(4, usage.Requests);
        Assert.Equal(1, usage.Errors);
        Assert.Equal(600, usage.InputTokens);
        Assert.Equal(100, usage.OutputTokens);
        Assert.Equal(2, usage.AverageDurationSeconds);
        Assert.Equal(0.5, usage.AverageTimeToFirstTokenSeconds);

        var load = Assert.Single(overview.Loads, l => l.Model == model);
        Assert.Equal(45, load.DurationSeconds);
        Assert.True(load.Succeeded);
    }

    [Fact]
    public async Task Overview_RejectsAnUnknownRange()
    {
        var response = await factory.CreateClient().GetAsync($"{Endpoint}?range=1y", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overview_IsForSovereignsOnly()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync(Endpoint, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Retention_DeletesRowsOlderThanThirtyDays()
    {
        var model = UniqueModel();
        var now = DateTimeOffset.UtcNow;
        var old = EpochMinutes.From(now.AddDays(-31));
        var recent = EpochMinutes.From(now.AddDays(-29));

        await SeedAsync(db =>
        {
            db.InferenceServerMinutes.AddRange(ServerMinute(model, old, 1, 1), ServerMinute(model, recent, 1, 1));
            db.InferenceUsageMinutes.Add(new InferenceUsageMinute { EpochMinute = old, Caller = "Agents", Model = model, Operation = "chat" });
        });

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        await scope.ServiceProvider.GetRequiredService<InferenceMetricsRetention>().SweepAsync(now, Ct);

        var db = scope.ServiceProvider.GetRequiredService<VigilDbContext>();
        Assert.Equal([recent], await db.InferenceServerMinutes.Where(r => r.Model == model).Select(r => r.EpochMinute).ToListAsync(Ct));
        Assert.False(await db.InferenceUsageMinutes.AnyAsync(r => r.Model == model, Ct));
    }
}
