using FluentAssertions;
using Sentium.Locus.Dtos;
using Sentium.Locus.Validation;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

public sealed class LocusValidatorTests
{
    [Fact]
    public void SaveLocation_RequiresAName()
    {
        var validator = new SaveLocationRequestValidator();

        validator.Validate(new SaveLocationRequest("  ", null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new SaveLocationRequest("Garage", null, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new SaveLocationRequest(new string('x', 101), null, null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveAsset_RequiresNameAndLocation()
    {
        var validator = new SaveAssetRequestValidator();

        validator.Validate(new SaveAssetRequest("Drill", Guid.NewGuid())).IsValid.Should().BeTrue();
        validator.Validate(new SaveAssetRequest("", Guid.NewGuid())).IsValid.Should().BeFalse();
        validator.Validate(new SaveAssetRequest("Drill", Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveAsset_ValidatesTags()
    {
        var validator = new SaveAssetRequestValidator();
        var location = Guid.NewGuid();

        validator.Validate(new SaveAssetRequest("Drill", location, Tags: ["tools", "power"])).IsValid.Should().BeTrue();
        validator.Validate(new SaveAssetRequest("Drill", location, Tags: ["a,b"])).IsValid.Should().BeFalse();
        validator.Validate(new SaveAssetRequest("Drill", location, Tags: [new string('t', 33)])).IsValid.Should().BeFalse();
        validator.Validate(new SaveAssetRequest("Drill", location, Tags: [.. Enumerable.Range(0, 21).Select(i => $"t{i}")])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveAsset_RejectsServiceDateBeforePurchase()
    {
        var validator = new SaveAssetRequestValidator();

        var request = new SaveAssetRequest("Drill", Guid.NewGuid(), PurchaseDate: new DateOnly(2025, 5, 1), LastServicedDate: new DateOnly(2024, 1, 1));

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SearchQuery_BoundsPageSize()
    {
        var validator = new AssetSearchQueryValidator();

        validator.Validate(new AssetSearchQuery { PageSize = 50 }).IsValid.Should().BeTrue();
        validator.Validate(new AssetSearchQuery { PageSize = 500 }).IsValid.Should().BeFalse();
        validator.Validate(new AssetSearchQuery { Q = new string('q', 201) }).IsValid.Should().BeFalse();
    }
}
