using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;
using Sentium.Codex.Contracts.Models;
using Sentium.Infrastructure.Security;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Locus.Data;
using Sentium.Locus.Dtos;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Knowledge;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Locus;

/// <summary>
/// Knowledge-base sync (driven directly through <see cref="KnowledgeSyncProcessor"/>; the background worker
/// is not running in tests) and the assistant-facing <see cref="IInventoryLookup"/>.
/// </summary>
public sealed class LocusKnowledgeAndLookupTests : IClassFixture<LocusApiFactory>
{
    private readonly LocusApiFactory _factory;
    private readonly HttpClient _client;

    public LocusKnowledgeAndLookupTests(LocusApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.Ingestion.ClearSubstitute();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<KnowledgeSyncOutcome> ProcessAsync(Guid assetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return await scope.ServiceProvider.GetRequiredService<KnowledgeSyncProcessor>().ProcessAsync(assetId, Ct);
    }

    private async Task<T> WithDbAsync<T>(Func<LocusDbContext, Task<T>> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<LocusDbContext>());
    }

    [Fact]
    public async Task SharedAsset_IsReplacedInTheKnowledgeBase_AsSharedInventoryContent()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Cordless drill", home.Id, Instructions: "Charge before storing"), Ct);
        var source = AssetKnowledgeDocumentBuilder.SourceFor(asset.Id);

        (await ProcessAsync(asset.Id)).Should().Be(KnowledgeSyncOutcome.Synced);

        // Replace, never append: the old copy is removed before both sections (identity, care) are written.
        Received.InOrder(() =>
        {
            _factory.Ingestion.RemoveBySourceAsync(source, null, Arg.Any<CancellationToken>());
            _factory.Ingestion.IngestAsync(Arg.Any<IngestionRequest>(), null, Arg.Any<CancellationToken>());
            _factory.Ingestion.IngestAsync(Arg.Any<IngestionRequest>(), null, Arg.Any<CancellationToken>());
        });
        await _factory.Ingestion.Received(2).IngestAsync(
            Arg.Is<IngestionRequest>(r => r.Source == source && r.Scope == KnowledgeScope.Shared && r.UserId == null
                                          && r.SourceType == IngestionSourceType.Inventory && r.Content.Contains("Cordless drill")),
            null, Arg.Any<CancellationToken>());

        var stored = await WithDbAsync(db => db.Assets.AsNoTracking().SingleAsync(a => a.Id == asset.Id, Ct));
        stored.KnowledgeContentHash.Should().NotBeNull();
        (await WithDbAsync(db => db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct))).Should().BeFalse();

        var dto = await _client.GetFromJsonAsync<AssetDto>($"/api/locus/assets/{asset.Id}", Ct);
        dto!.KnowledgeStatus.Should().Be(AssetKnowledgeStatus.Synced);
    }

    [Fact]
    public async Task UnchangedContent_IsNotReingested()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Kettle", home.Id), Ct);
        await ProcessAsync(asset.Id);
        _factory.Ingestion.ClearReceivedCalls();

        await _client.PostAsync($"/api/locus/assets/{asset.Id}/knowledge-sync", null, Ct);

        (await ProcessAsync(asset.Id)).Should().Be(KnowledgeSyncOutcome.Unchanged);
        await _factory.Ingestion.DidNotReceiveWithAnyArgs().IngestAsync(default!, default, Ct);
    }

    [Fact]
    public async Task PrivateOrDeletedAssets_AreRemovedFromTheKnowledgeBase()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Router", home.Id), Ct);
        await ProcessAsync(asset.Id);
        _factory.Ingestion.ClearReceivedCalls();

        await _client.PutAsJsonAsync($"/api/locus/assets/{asset.Id}", new SaveAssetRequest("Router", home.Id, IsAgentAccessible: false), Ct);
        (await ProcessAsync(asset.Id)).Should().Be(KnowledgeSyncOutcome.Removed);

        await _factory.Ingestion.Received(1).RemoveBySourceAsync(AssetKnowledgeDocumentBuilder.SourceFor(asset.Id), null, Arg.Any<CancellationToken>());
        await _factory.Ingestion.DidNotReceiveWithAnyArgs().IngestAsync(default!, default, Ct);
        (await WithDbAsync(db => db.Assets.AsNoTracking().SingleAsync(a => a.Id == asset.Id, Ct))).KnowledgeContentHash.Should().BeNull();

        await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct);
        (await ProcessAsync(asset.Id)).Should().Be(KnowledgeSyncOutcome.Removed);
    }

    [Fact]
    public async Task FailedIngestion_IsRescheduledWithBackoff()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Speaker", home.Id), Ct);
        _factory.Ingestion.IngestAsync(Arg.Any<IngestionRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("embedding server unavailable"));

        (await ProcessAsync(asset.Id)).Should().Be(KnowledgeSyncOutcome.Failed);

        var entry = await WithDbAsync(db => db.KnowledgeSyncEntries.AsNoTracking().SingleAsync(e => e.AssetId == asset.Id, Ct));
        entry.Attempts.Should().Be(1);
        entry.Status.Should().Be(KnowledgeSyncStatus.Pending);
        entry.LastError.Should().Contain("embedding server unavailable");
        entry.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow);

        var dto = await _client.GetFromJsonAsync<AssetDto>($"/api/locus/assets/{asset.Id}", Ct);
        dto!.KnowledgeError.Should().Contain("embedding server unavailable");
    }

    [Fact]
    public async Task AnEditDuringIngestion_KeepsTheEntryQueued()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Camera", home.Id), Ct);

        _factory.Ingestion.IngestAsync(Arg.Any<IngestionRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await using var scope = _factory.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<KnowledgeSyncOutbox>().EnqueueAsync([asset.Id], CancellationToken.None);
            });

        (await ProcessAsync(asset.Id)).Should().Be(KnowledgeSyncOutcome.Synced);

        (await WithDbAsync(db => db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct))).Should().BeTrue();
    }

    [Fact]
    public async Task InventoryLookup_NeverReturnsPrivateAssets()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var home = await _client.CreateLocationAsync($"Home{token}", ct: Ct);
        var garage = await _client.CreateLocationAsync("Garage", home.Id, Ct);
        var shared = await _client.CreateAssetAsync(new SaveAssetRequest($"Drill {token}", garage.Id, AgentInstructions: "Wear goggles"), Ct);
        var hidden = await _client.CreateAssetAsync(new SaveAssetRequest($"Safe {token}", garage.Id, IsAgentAccessible: false), Ct);

        var lookup = _factory.Services.GetRequiredService<IInventoryLookup>();

        var found = await lookup.FindAssetsAsync(new InventoryAssetQuery(token), Ct);
        found.Should().ContainSingle().Which.Id.Should().Be(shared.Id);
        found[0].LocationPath.Should().Be($"Home{token} › Garage");

        (await lookup.GetAssetAsync(hidden.Id, Ct)).Should().BeNull();
        (await lookup.FindAssetsByNameAsync($"safe {token}", ct: Ct)).Should().BeEmpty();

        var detail = await lookup.GetAssetAsync(shared.Id, Ct);
        detail!.AssistantNotes.Should().Be("Wear goggles");

        var contents = await lookup.GetLocationContentsAsync($"Home{token}/Garage", ct: Ct);
        contents.Status.Should().Be(InventoryLocationLookupStatus.Found);
        contents.Assets.Should().ContainSingle().Which.Id.Should().Be(shared.Id);

        var nested = await lookup.GetLocationContentsAsync($"Home{token}", includeNested: true, ct: Ct);
        nested.Assets.Select(a => a.Id).Should().Equal(shared.Id);
        nested.SubLocations.Should().ContainSingle().Which.Name.Should().Be("Garage");

        (await lookup.FindAssetsAsync(new InventoryAssetQuery(null, Location: $"Home{token}"), Ct)).Should().ContainSingle();
        (await lookup.FindAssetsAsync(new InventoryAssetQuery("drill", Location: "Nowhere-" + token), Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Reconciliation_QueuesSharedAssetsWhoseContentIsOutOfDate()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        var asset = await _client.CreateAssetAsync(new SaveAssetRequest("Projector", home.Id), Ct);
        await ProcessAsync(asset.Id);

        // Simulate an edit that never reached the outbox (e.g. written while the app was down).
        await WithDbAsync(db => db.Assets.Where(a => a.Id == asset.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.KnowledgeContentHash, "stale"), Ct));

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var queued = await scope.ServiceProvider.GetRequiredService<KnowledgeSyncProcessor>().ReconcileAsync(Ct);

        queued.Should().BeGreaterThan(0);
        (await WithDbAsync(db => db.KnowledgeSyncEntries.AnyAsync(e => e.AssetId == asset.Id, Ct))).Should().BeTrue();
    }
}
