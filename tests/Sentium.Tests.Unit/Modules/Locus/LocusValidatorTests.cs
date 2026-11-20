using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class LocusValidatorTests
{
    [Fact]
    public void SaveLocation_RequiresAName()
    {
        var validator = new SaveLocationRequestValidator();

        Assert.False(validator.Validate(new SaveLocationRequest("  ", null, null, null)).IsValid);
        Assert.True(validator.Validate(new SaveLocationRequest("Garage", null, null, null)).IsValid);
        Assert.False(validator.Validate(new SaveLocationRequest(new string('x', 101), null, null, null)).IsValid);
    }

    [Fact]
    public void SaveAsset_RequiresNameAndLocation()
    {
        var validator = new SaveAssetRequestValidator();

        Assert.True(validator.Validate(new SaveAssetRequest("Drill", Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(new SaveAssetRequest("", Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(new SaveAssetRequest("Drill", Guid.Empty)).IsValid);
    }

    [Fact]
    public void SaveAsset_ValidatesTags()
    {
        var validator = new SaveAssetRequestValidator();
        var location = Guid.NewGuid();

        Assert.True(validator.Validate(new SaveAssetRequest("Drill", location, Tags: ["tools", "power"])).IsValid);
        Assert.False(validator.Validate(new SaveAssetRequest("Drill", location, Tags: ["a,b"])).IsValid);
        Assert.False(validator.Validate(new SaveAssetRequest("Drill", location, Tags: [new string('t', 33)])).IsValid);
        Assert.False(validator.Validate(new SaveAssetRequest("Drill", location, Tags: [.. Enumerable.Range(0, 21).Select(i => $"t{i}")])).IsValid);
    }

    [Fact]
    public void SaveAsset_RejectsServiceDateBeforePurchase()
    {
        var validator = new SaveAssetRequestValidator();

        var request = new SaveAssetRequest("Drill", Guid.NewGuid(), PurchaseDate: new DateOnly(2025, 5, 1), LastServicedDate: new DateOnly(2024, 1, 1));

        Assert.False(validator.Validate(request).IsValid);
    }

    [Fact]
    public void SearchQuery_BoundsPageSize()
    {
        var validator = new AssetSearchQueryValidator();

        Assert.True(validator.Validate(new AssetSearchQuery { PageSize = 50 }).IsValid);
        Assert.False(validator.Validate(new AssetSearchQuery { PageSize = 500 }).IsValid);
        Assert.False(validator.Validate(new AssetSearchQuery { Q = new string('q', 201) }).IsValid);
    }
}
