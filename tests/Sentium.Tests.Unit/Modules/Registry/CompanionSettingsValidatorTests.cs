using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class CompanionSettingsValidatorTests
{
    private static Task<bool> IsValidAsync(CompanionSettings settings) =>
        SettingsValidation.IsValidAsync(settings, new CompanionSettingsValidator());

    [Fact]
    public async Task Defaults_Pass_WithTheCompanionAndVoiceOn()
    {
        var defaults = new CompanionSettings();

        Assert.True(await IsValidAsync(defaults));
        Assert.True(defaults.IsCompanionEnabled);
        Assert.True(defaults.IsVoiceEnabled);
        Assert.False(defaults.IsReviewBeforeSendingEnabled);
        Assert.False(defaults.IsSpeakTypedRepliesEnabled);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.3)]
    [InlineData(2.0)]
    public async Task SpeakingRate_InRange_Passes(double rate)
    {
        Assert.True(await IsValidAsync(new CompanionSettings { SpeakingRate = rate }));
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(2.01)]
    [InlineData(-1)]
    public async Task SpeakingRate_OutOfRange_Fails(double rate)
    {
        Assert.False(await IsValidAsync(new CompanionSettings { SpeakingRate = rate }));
    }

    [Theory]
    [InlineData("af_heart")]
    [InlineData("bm-george_2")]
    public async Task Voice_Id_Passes(string voice)
    {
        Assert.True(await IsValidAsync(new CompanionSettings { Voice = voice }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a/b")]
    public async Task Voice_ThatIsNotAnId_Fails(string voice)
    {
        Assert.False(await IsValidAsync(new CompanionSettings { Voice = voice }));
    }

    [Fact]
    public async Task Voice_LongerThanTheLimit_Fails()
    {
        var voice = new string('a', CompanionSettings.MaxVoiceIdLength + 1);

        Assert.False(await IsValidAsync(new CompanionSettings { Voice = voice }));
    }
}
