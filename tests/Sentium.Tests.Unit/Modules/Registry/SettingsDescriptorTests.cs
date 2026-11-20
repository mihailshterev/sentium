using System.Text.Json;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class SettingsDescriptorTests
{
    private readonly SettingsDescriptor<VigilSettings> _sut = new(SettingsKeys.Vigil, SettingsScope.Household);

    [Fact]
    public void Read_ReturnsDefaults_WhenTheRowHasNoValueForTheKey()
    {
        var value = Assert.IsType<VigilSettings>(_sut.Read(new Dictionary<string, JsonElement>()));

        Assert.Equal(new VigilSettings().PollIntervalSeconds, value.PollIntervalSeconds);
    }

    [Fact]
    public void Read_FillsFieldsMissingFromStoredJson_WithDefaults()
    {
        var settings = new Dictionary<string, JsonElement>
        {
            [SettingsKeys.Vigil] = JsonDocument.Parse("""{"pollIntervalSeconds":30}""").RootElement.Clone()
        };

        var value = Assert.IsType<VigilSettings>(_sut.Read(settings));

        Assert.Equal(30, value.PollIntervalSeconds);
        Assert.Equal(new VigilSettings().ProbeTimeoutSeconds, value.ProbeTimeoutSeconds);
    }

    [Fact]
    public void Write_StoresUnderTheKey_AndKeepsOtherKeys()
    {
        var unknown = JsonDocument.Parse("""{"legacy":true}""").RootElement.Clone();
        var settings = new Dictionary<string, JsonElement> { ["retired-group"] = unknown };

        _sut.Write(settings, new VigilSettings { PollIntervalSeconds = 42 });

        Assert.Equal(42, settings[SettingsKeys.Vigil].GetProperty("pollIntervalSeconds").GetInt32());
        Assert.True(settings.ContainsKey("retired-group"));
    }
}
