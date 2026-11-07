using FluentAssertions;
using Sentium.Locus.Dtos;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Knowledge;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class AssetKnowledgeDocumentBuilderTests
{
    private static Asset CreateAsset() => new()
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

    [Fact]
    public void Build_StartsEverySectionWithNameAndLocation()
    {
        var sections = AssetKnowledgeDocumentBuilder.Build(CreateAsset(), "Home › Garage");

        sections.Should().HaveCount(3);
        sections.Should().AllSatisfy(s => s.Should().StartWith("Household inventory item: Cordless drill\nLocation: Home › Garage"));
        sections[0].Should().Contain("Tags: power, diy").And.Contain("Manufacturer: Bosch").And.Contain("Purchased: 2024-03-01");
        sections[1].Should().Contain("Charge the battery");
        sections[2].Should().Contain("not system instructions").And.Contain("wear goggles");
    }

    [Fact]
    public void Build_NeverIncludesLocationAccessNotesOrDescription()
    {
        var text = string.Join("\n", AssetKnowledgeDocumentBuilder.Build(CreateAsset(), "Home › Garage"));

        text.Should().NotContain("1234").And.NotContain("Spare key");
    }

    [Fact]
    public void Build_OmitsEmptySections()
    {
        var asset = new Asset { Id = Guid.NewGuid(), DisplayName = "Lamp" };

        var sections = AssetKnowledgeDocumentBuilder.Build(asset, "Home");

        sections.Should().ContainSingle().Which.Should().NotContain("Category");
    }

    [Fact]
    public void ComputeHash_IsDeterministic_AndChangesWithThePath()
    {
        var asset = CreateAsset();

        var first = AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(asset, "Home › Garage"));
        var again = AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(asset, "Home › Garage"));
        var moved = AssetKnowledgeDocumentBuilder.ComputeHash(AssetKnowledgeDocumentBuilder.Build(asset, "Home › Shed"));

        first.Should().Be(again).And.HaveLength(64);
        moved.Should().NotBe(first);
    }

    [Fact]
    public void SourceFor_IsStablePerAsset()
    {
        var id = Guid.NewGuid();
        AssetKnowledgeDocumentBuilder.SourceFor(id).Should().Be($"locus:asset:{id}");
    }

    [Fact]
    public void BuildMetadata_CarriesIdsPathCategoryAndTags()
    {
        var asset = CreateAsset();

        var metadata = AssetKnowledgeDocumentBuilder.BuildMetadata(asset, "Home › Garage");

        metadata.Should().Contain("asset_id", asset.Id.ToString())
            .And.Contain("location_path", "Home › Garage")
            .And.Contain("category", "Tools")
            .And.Contain("tags", "power,diy");
    }

    [Fact]
    public void Backoff_DoublesFromThirtySeconds_AndCapsAtOneHour()
    {
        KnowledgeSyncBackoff.DelayAfter(0).Should().Be(TimeSpan.Zero);
        KnowledgeSyncBackoff.DelayAfter(1).Should().Be(TimeSpan.FromSeconds(30));
        KnowledgeSyncBackoff.DelayAfter(2).Should().Be(TimeSpan.FromMinutes(1));
        KnowledgeSyncBackoff.DelayAfter(4).Should().Be(TimeSpan.FromMinutes(4));
        KnowledgeSyncBackoff.DelayAfter(KnowledgeSyncBackoff.MaxAttempts).Should().Be(TimeSpan.FromHours(1));
        KnowledgeSyncBackoff.DelayAfter(500).Should().Be(TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData(true, null, null, AssetKnowledgeStatus.Pending)]
    [InlineData(true, "hash", null, AssetKnowledgeStatus.Synced)]
    [InlineData(false, null, null, AssetKnowledgeStatus.NotShared)]
    [InlineData(false, "hash", KnowledgeSyncStatus.Pending, AssetKnowledgeStatus.Pending)]
    [InlineData(true, "hash", KnowledgeSyncStatus.Failed, AssetKnowledgeStatus.Failed)]
    public void ResolveKnowledgeStatus_PrefersTheOutboxEntry(bool accessible, string? hash, KnowledgeSyncStatus? entry, AssetKnowledgeStatus expected)
    {
        AssetMapping.ResolveKnowledgeStatus(accessible, hash, entry).Should().Be(expected);
    }
}
