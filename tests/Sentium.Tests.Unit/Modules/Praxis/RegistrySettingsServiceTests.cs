using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Core.Constants;
using Sentium.Praxis.Features.Registry;
using Sentium.Registry.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

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
    public async Task GetAsync_CombinesUserHarnessWithGlobalOllamaSettings()
    {
        _settings.GetAsync(SettingsKeys.Harness, UserId, Arg.Any<CancellationToken>())
            .Returns(Envelope(SettingsKeys.Harness, new { userHarnessPrompt = "be brief", isBuiltInHarnessEnabled = false, isPromptEnhancementEnabled = true }));
        _settings.GetAsync(SettingsKeys.Ollama, null, Arg.Any<CancellationToken>())
            .Returns(Envelope(SettingsKeys.Ollama, new { defaultModel = "qwen3:4b", agentTemperature = 0.7f, agentContextWindow = 8192 }));

        var snapshot = await _sut.GetAsync(UserId, Ct);

        snapshot.Harness.Should().Be(new HarnessSettingsSnapshot("be brief", IsBuiltInHarnessEnabled: false, IsPromptEnhancementEnabled: true));
        snapshot.Ollama.Should().Be(new OllamaSettingsSnapshot("qwen3:4b", 0.7f, 8192));
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenHarnessCannotBeRead()
    {
        _settings.GetAsync(SettingsKeys.Harness, UserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));

        var snapshot = await _sut.GetAsync(UserId, Ct);

        snapshot.Should().Be(SettingsSnapshot.Default);
    }

    [Fact]
    public async Task GetAsync_KeepsHarness_WhenOnlyOllamaSettingsFail()
    {
        _settings.GetAsync(SettingsKeys.Harness, UserId, Arg.Any<CancellationToken>())
            .Returns(Envelope(SettingsKeys.Harness, new { userHarnessPrompt = "custom", isBuiltInHarnessEnabled = true }));
        _settings.GetAsync(SettingsKeys.Ollama, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("unavailable"));

        var snapshot = await _sut.GetAsync(UserId, Ct);

        snapshot.Harness.UserHarnessPrompt.Should().Be("custom");
        snapshot.Ollama.Should().BeNull();
    }
}
