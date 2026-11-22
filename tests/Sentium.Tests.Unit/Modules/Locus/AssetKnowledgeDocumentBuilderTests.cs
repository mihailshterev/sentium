using Sentium.Infrastructure.BackgroundWork;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Knowledge;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class AssetKnowledgeDocumentBuilderTests
{
    private static Asset CreateAsset()
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            DisplayName = "Cordless drill",
            Category = "Tools",
            Manufacturer = "Bosch",
            ModelNumber = "GSR 12V",
            Tags = ["power", "diy"],
            PurchaseDate = new DateOnly(2024, 3, 1),
            Instructions = "Charge the battery fully before storing.",
            AgentInstructions = "Remind people to wear goggles.",
            Location = new Location { Name = "Garage", AccessNotes = "Alarm code 1234", Description = "Spare key under the mat" }
        };
    }

    [Fact]
    public void Build_StartsEverySectionWithNameAndLocation()
    {
        var sections = AssetKnowledgeDocumentBuilder.Build(CreateAsset(), "Home › Garage");

        Assert.Equal(3, sections.Count);
        Assert.All(sections, s => Assert.StartsWith("Household inventory item: Cordless drill\nLocation: Home › Garage", s, StringComparison.Ordinal));
        Assert.Contains("Tags: power, diy", sections[0], StringComparison.Ordinal);
        Assert.Contains("Manufacturer: Bosch", sections[0], StringComparison.Ordinal);
        Assert.Contains("Purchased: 2024-03-01", sections[0], StringComparison.Ordinal);
        Assert.Contains("Charge the battery", sections[1], StringComparison.Ordinal);
        Assert.Contains("not system instructions", sections[2], StringComparison.Ordinal);
        Assert.Contains("wear goggles", sections[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NeverIncludesLocationAccessNotesOrDescription()
    {
        var text = string.Join("\n", AssetKnowledgeDocumentBuilder.Build(CreateAsset(), "Home › Garage"));

        Assert.DoesNotContain("1234", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Spare key", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_OmitsEmptySections()
    {
        var asset = new Asset { Id = Guid.NewGuid(), DisplayName = "Lamp" };

        var sections = AssetKnowledgeDocumentBuilder.Build(asset, "Home");

        Assert.DoesNotContain("Category", Assert.Single(sections), StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeHash_IsDeterministic_AndChangesWithThePath()
    {
        var asset = CreateAsset();

        var first = AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(asset, "Home › Garage"));
        var again = AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(asset, "Home › Garage"));
        var moved = AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(asset, "Home › Shed"));

        Assert.Equal(again, first);
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, moved);
    }

    [Fact]
    public void SourceFor_IsStablePerAsset()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"locus:asset:{id}", AssetKnowledgeDocumentBuilder.SourceFor(id));
    }

    [Fact]
    public void BuildMetadata_CarriesIdsPathCategoryAndTags()
    {
        var asset = CreateAsset();

        var metadata = AssetKnowledgeDocumentBuilder.BuildMetadata(asset, "Home › Garage");

        Assert.Equal(asset.Id.ToString(), Assert.Contains("asset_id", metadata));
        Assert.Equal("Home › Garage", Assert.Contains("location_path", metadata));
        Assert.Equal("Tools", Assert.Contains("category", metadata));
        Assert.Equal("power,diy", Assert.Contains("tags", metadata));
    }

    [Fact]
    public void Backoff_DoublesFromThirtySeconds_AndCapsAtOneHour()
    {
        Assert.Equal(TimeSpan.Zero, RetryBackoff.DelayAfter(0));
        Assert.Equal(TimeSpan.FromSeconds(30), RetryBackoff.DelayAfter(1));
        Assert.Equal(TimeSpan.FromMinutes(1), RetryBackoff.DelayAfter(2));
        Assert.Equal(TimeSpan.FromMinutes(4), RetryBackoff.DelayAfter(4));
        Assert.Equal(TimeSpan.FromHours(1), RetryBackoff.DelayAfter(RetryBackoff.MaxAttempts));
        Assert.Equal(TimeSpan.FromHours(1), RetryBackoff.DelayAfter(500));
    }

    [Theory]
    [InlineData(true, null, null, nameof(AssetKnowledgeStatus.Pending))]
    [InlineData(true, "hash", null, nameof(AssetKnowledgeStatus.Synced))]
    [InlineData(false, null, null, nameof(AssetKnowledgeStatus.NotShared))]
    [InlineData(false, "hash", nameof(KnowledgeSyncStatus.Pending), nameof(AssetKnowledgeStatus.Pending))]
    [InlineData(true, "hash", nameof(KnowledgeSyncStatus.Failed), nameof(AssetKnowledgeStatus.Failed))]
    public void ResolveKnowledgeStatus_PrefersTheOutboxEntry(bool accessible, string? hash, string? entry, string expected)
    {
        var entryStatus = entry is null ? (KnowledgeSyncStatus?)null : Enum.Parse<KnowledgeSyncStatus>(entry);

        Assert.Equal(Enum.Parse<AssetKnowledgeStatus>(expected), AssetMapping.ResolveKnowledgeStatus(accessible, hash, entryStatus));
    }
}
