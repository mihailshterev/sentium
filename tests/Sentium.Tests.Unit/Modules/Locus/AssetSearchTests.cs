using FluentAssertions;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class AssetSearchTests
{
    [Fact]
    public void ParseTerms_LowercasesDeduplicatesAndCaps()
    {
        AssetSearch.ParseTerms("  Garage  DRILL garage ").Should().Equal("garage", "drill");
        AssetSearch.ParseTerms(null).Should().BeEmpty();
        AssetSearch.ParseTerms("   ").Should().BeEmpty();
        AssetSearch.ParseTerms("a b c d e f g h i j").Should().HaveCount(8);
    }

    [Theory]
    [InlineData("drill", "%drill%")]
    [InlineData("100%", "%100\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("c:\\temp", "%c:\\\\temp%")]
    public void ToContainsPattern_EscapesLikeWildcards(string term, string expected)
    {
        AssetSearch.ToContainsPattern(term).Should().Be(expected);
    }

    [Fact]
    public void BuildSearchText_CombinesSearchableFieldsAndTags_LowerCased()
    {
        var asset = new Asset
        {
            DisplayName = "Cordless Drill",
            Category = "Tools",
            Manufacturer = "Bosch",
            ModelNumber = "GSR-12V",
            SerialNumber = "SN123",
            PhysicalDescription = "Green case",
            Instructions = "not searchable",
            Tags = ["power", "garage"]
        };

        var text = AssetSearch.BuildSearchText(asset);

        text.Should().Contain("cordless drill").And.Contain("bosch").And.Contain("gsr-12v").And.Contain("sn123")
            .And.Contain("green case").And.Contain("power").And.Contain("garage");
        text.Should().NotContain("not searchable");
        text.Should().Be(text.ToLowerInvariant());
    }

    [Fact]
    public void TagNormalizer_TrimsCollapsesLowercasesAndDeduplicates()
    {
        TagNormalizer.Normalize([" Power  Tools ", "power tools", "", "  ", "Garage"]).Should().Equal("power tools", "garage");
        TagNormalizer.Normalize(null).Should().BeEmpty();
        TagNormalizer.NormalizeOne("  ").Should().BeNull();
    }

    [Fact]
    public void LocationNames_NormalizeIgnoresCaseAndExtraWhitespace()
    {
        LocationNames.Normalize("  Shelf   B ").Should().Be("shelf b");
        LocationNames.Clean("  Shelf   B ").Should().Be("Shelf B");
    }
}
