using FluentAssertions;
using Sentium.Registry.Api.Validation;
using Sentium.Registry.Core.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Registry;

public sealed class WatchdogSettingsValidatorTests
{
    private readonly WatchdogSettingsValidator _sut = new();

    [Fact]
    public void Valid_Defaults_Pass()
    {
        _sut.Validate(new WatchdogSettings()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(4)]      // below min
    [InlineData(3601)]   // above max
    public void Invalid_PollInterval_Fails(int seconds)
    {
        var result = _sut.Validate(new WatchdogSettings { PollIntervalSeconds = seconds });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ProbeTimeout_GreaterThanPollInterval_Fails()
    {
        var result = _sut.Validate(new WatchdogSettings { PollIntervalSeconds = 5, ProbeTimeoutSeconds = 10 });
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Invalid_ProbeTimeout_Fails(int seconds)
    {
        var result = _sut.Validate(new WatchdogSettings { ProbeTimeoutSeconds = seconds });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_SampleHistorySize_Fails()
    {
        _sut.Validate(new WatchdogSettings { SampleHistorySize = 5 }).IsValid.Should().BeFalse();
        _sut.Validate(new WatchdogSettings { SampleHistorySize = 500 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_ConsecutiveFailures_Fails()
    {
        _sut.Validate(new WatchdogSettings { ConsecutiveFailuresToOpenIncident = 0 }).IsValid.Should().BeFalse();
    }
}
