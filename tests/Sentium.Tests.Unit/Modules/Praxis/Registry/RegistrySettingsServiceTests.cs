using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Core.Constants;
using Sentium.Praxis.Features.Registry;
using Sentium.Registry.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Registry;

public sealed class RegistrySettingsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly RegistrySettingsService _sut;

    public RegistrySettingsServiceTests()
    {
        _sut = new RegistrySettingsService(_settings, NullLogger<RegistrySettingsService>.Instance);
    }

    private static SettingsEnvelope Envelope(string key, object value) =>
        new(key, JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)), DateTimeOffset.UtcNow, null);

    [Fact]
    public async Task GetAsync_CombinesUserHarnessWithGlobalInferenceSettings()
    {
        _settings.GetAsync(SettingsKeys.Harness, UserId, Arg.Any<CancellationToken>())
            .Returns(Envelope(SettingsKeys.Harness, new { userHarnessPrompt = "be brief", isBuiltInHarnessEnabled = false, isPromptEnhancementEnabled = true }));
        _settings.GetAsync(SettingsKeys.Inference, null, Arg.Any<CancellationToken>())
            .Returns(Envelope(SettingsKeys.Inference, new { defaultModel = "qwen3:4b", agentTemperature = 0.7f }));

        var snapshot = await _sut.GetAsync(UserId, Ct);

        Assert.Equal(new HarnessSettingsSnapshot("be brief", IsBuiltInHarnessEnabled: false, IsPromptEnhancementEnabled: true), snapshot.Harness);
        Assert.Equal(new InferenceSettingsSnapshot("qwen3:4b", 0.7f), snapshot.Inference);
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenHarnessCannotBeRead()
    {
        _settings.GetAsync(SettingsKeys.Harness, UserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));

        var snapshot = await _sut.GetAsync(UserId, Ct);

        Assert.Equal(SettingsSnapshot.Default, snapshot);
    }

    [Fact]
    public async Task GetAsync_KeepsHarness_WhenOnlyInferenceSettingsFail()
    {
        _settings.GetAsync(SettingsKeys.Harness, UserId, Arg.Any<CancellationToken>())
            .Returns(Envelope(SettingsKeys.Harness, new { userHarnessPrompt = "custom", isBuiltInHarnessEnabled = true }));
        _settings.GetAsync(SettingsKeys.Inference, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("unavailable"));

        var snapshot = await _sut.GetAsync(UserId, Ct);

        Assert.Equal("custom", snapshot.Harness.UserHarnessPrompt);
        Assert.Null(snapshot.Inference);
    }
}
