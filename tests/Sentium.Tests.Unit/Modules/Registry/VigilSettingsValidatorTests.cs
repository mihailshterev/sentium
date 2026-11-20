using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class VigilSettingsValidatorTests
{
    private readonly VigilSettingsValidator _sut = new();

    [Fact]
    public void Valid_Defaults_Pass()
    {
        Assert.True(_sut.Validate(new VigilSettings()).IsValid);
    }

    [Theory]
    [InlineData(4)]      // below min
    [InlineData(3601)]   // above max
    public void Invalid_PollInterval_Fails(int seconds)
    {
        var result = _sut.Validate(new VigilSettings { PollIntervalSeconds = seconds });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ProbeTimeout_GreaterThanPollInterval_Fails()
    {
        var result = _sut.Validate(new VigilSettings { PollIntervalSeconds = 5, ProbeTimeoutSeconds = 10 });
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Invalid_ProbeTimeout_Fails(int seconds)
    {
        var result = _sut.Validate(new VigilSettings { ProbeTimeoutSeconds = seconds });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_SampleHistorySize_Fails()
    {
        Assert.False(_sut.Validate(new VigilSettings { SampleHistorySize = 5 }).IsValid);
        Assert.False(_sut.Validate(new VigilSettings { SampleHistorySize = 500 }).IsValid);
    }

    [Fact]
    public void Invalid_ConsecutiveFailures_Fails()
    {
        Assert.False(_sut.Validate(new VigilSettings { ConsecutiveFailuresToOpenIncident = 0 }).IsValid);
    }
}
