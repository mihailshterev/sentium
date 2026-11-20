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

        Assert.True(_sut.Validate(defaults).IsValid);
        Assert.True(defaults.IsCompanionEnabled);
        Assert.True(defaults.IsVoiceEnabled);
        Assert.False(defaults.IsReviewBeforeSendingEnabled);
        Assert.False(defaults.IsSpeakTypedRepliesEnabled);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.3)]
    [InlineData(2.0)]
    public void SpeakingRate_InRange_Passes(double rate)
    {
        Assert.True(_sut.Validate(new CompanionSettings { SpeakingRate = rate }).IsValid);
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(2.01)]
    [InlineData(-1)]
    public void SpeakingRate_OutOfRange_Fails(double rate)
    {
        Assert.False(_sut.Validate(new CompanionSettings { SpeakingRate = rate }).IsValid);
    }

    [Theory]
    [InlineData("af_heart")]
    [InlineData("bm-george_2")]
    public void Voice_Id_Passes(string voice)
    {
        Assert.True(_sut.Validate(new CompanionSettings { Voice = voice }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a/b")]
    public void Voice_ThatIsNotAnId_Fails(string voice)
    {
        Assert.False(_sut.Validate(new CompanionSettings { Voice = voice }).IsValid);
    }

    [Fact]
    public void Voice_LongerThanTheLimit_Fails()
    {
        var voice = new string('a', CompanionSettings.MaxVoiceIdLength + 1);

        Assert.False(_sut.Validate(new CompanionSettings { Voice = voice }).IsValid);
    }
}
