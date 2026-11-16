using FluentAssertions;
using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class CompanionSettingsValidatorTests
{
    private readonly CompanionSettingsValidator _sut = new();

    [Fact]
    public void Defaults_Pass_WithTheCompanionAndVoiceOn()
    {
        var defaults = new CompanionSettings();

        _sut.Validate(defaults).IsValid.Should().BeTrue();
        defaults.IsCompanionEnabled.Should().BeTrue();
        defaults.IsVoiceEnabled.Should().BeTrue();
        defaults.IsReviewBeforeSendingEnabled.Should().BeFalse();
        defaults.IsSpeakTypedRepliesEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.3)]
    [InlineData(2.0)]
    public void SpeakingRate_InRange_Passes(double rate)
    {
        _sut.Validate(new CompanionSettings { SpeakingRate = rate }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(2.01)]
    [InlineData(-1)]
    public void SpeakingRate_OutOfRange_Fails(double rate)
    {
        _sut.Validate(new CompanionSettings { SpeakingRate = rate }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("af_heart")]
    [InlineData("bm-george_2")]
    public void Voice_Id_Passes(string voice)
    {
        _sut.Validate(new CompanionSettings { Voice = voice }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a/b")]
    public void Voice_ThatIsNotAnId_Fails(string voice)
    {
        _sut.Validate(new CompanionSettings { Voice = voice }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Voice_LongerThanTheLimit_Fails()
    {
        var voice = new string('a', CompanionSettings.MaxVoiceIdLength + 1);

        _sut.Validate(new CompanionSettings { Voice = voice }).IsValid.Should().BeFalse();
    }
}
