using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class VigilSettingsValidatorTests
{
    private static Task<bool> IsValidAsync(VigilSettings settings) =>
        SettingsValidation.IsValidAsync(settings, new VigilSettingsValidator());

    [Fact]
    public async Task Valid_Defaults_Pass()
    {
        Assert.True(await IsValidAsync(new VigilSettings()));
    }

    [Theory]
    [InlineData(4)]      // below min
    [InlineData(3601)]   // above max
    public async Task Invalid_PollInterval_Fails(int seconds)
    {
        Assert.False(await IsValidAsync(new VigilSettings { PollIntervalSeconds = seconds }));
    }

    [Fact]
    public async Task ProbeTimeout_GreaterThanPollInterval_Fails()
    {
        Assert.False(await IsValidAsync(new VigilSettings { PollIntervalSeconds = 5, ProbeTimeoutSeconds = 10 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Invalid_ProbeTimeout_Fails(int seconds)
    {
        Assert.False(await IsValidAsync(new VigilSettings { ProbeTimeoutSeconds = seconds }));
    }

    [Fact]
    public async Task Invalid_SampleHistorySize_Fails()
    {
        Assert.False(await IsValidAsync(new VigilSettings { SampleHistorySize = 5 }));
        Assert.False(await IsValidAsync(new VigilSettings { SampleHistorySize = 500 }));
    }

    [Fact]
    public async Task Invalid_ConsecutiveFailures_Fails()
    {
        Assert.False(await IsValidAsync(new VigilSettings { ConsecutiveFailuresToOpenIncident = 0 }));
    }
}
