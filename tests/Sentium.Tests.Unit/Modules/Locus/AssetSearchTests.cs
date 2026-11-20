using Sentium.Core.Text;
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
        Assert.Equal(new string[] { "garage", "drill" }, AssetSearch.ParseTerms("  Garage  DRILL garage "));
        Assert.Empty(AssetSearch.ParseTerms(null));
        Assert.Empty(AssetSearch.ParseTerms("   "));
        Assert.Equal(8, AssetSearch.ParseTerms("a b c d e f g h i j").Count);
    }

    [Theory]
    [InlineData("drill", "%drill%")]
    [InlineData("100%", "%100\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("c:\\temp", "%c:\\\\temp%")]
    public void ToContainsPattern_EscapesLikeWildcards(string term, string expected)
    {
        Assert.Equal(expected, AssetSearch.ToContainsPattern(term));
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

        Assert.Contains("cordless drill", text, StringComparison.Ordinal);
        Assert.Contains("bosch", text, StringComparison.Ordinal);
        Assert.Contains("gsr-12v", text, StringComparison.Ordinal);
        Assert.Contains("sn123", text, StringComparison.Ordinal);
        Assert.Contains("green case", text, StringComparison.Ordinal);
        Assert.Contains("power", text, StringComparison.Ordinal);
        Assert.Contains("garage", text, StringComparison.Ordinal);
        Assert.DoesNotContain("not searchable", text, StringComparison.Ordinal);
        Assert.Equal(text.ToLowerInvariant(), text);
    }

    [Fact]
    public void TagNormalizer_TrimsCollapsesLowercasesAndDeduplicates()
    {
        Assert.Equal(new string[] { "power tools", "garage" }, TagNormalizer.Normalize([" Power  Tools ", "power tools", "", "  ", "Garage"]));
        Assert.Empty(TagNormalizer.Normalize(null));
        Assert.Null(TagNormalizer.NormalizeOne("  "));
    }

    [Fact]
    public void LocationNames_NormalizeIgnoresCaseAndExtraWhitespace()
    {
        Assert.Equal("shelf b", LocationNames.Normalize("  Shelf   B "));
        Assert.Equal("Shelf B", LocationNames.Clean("  Shelf   B "));
    }
}
