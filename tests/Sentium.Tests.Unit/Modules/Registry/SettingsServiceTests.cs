using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Registry.Features.Settings;
using Sentium.Registry.Entities;
using Sentium.Core.Constants;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class SettingsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();

    private readonly SettingsCatalog _catalog = new(new ISettingsDescriptor[]
    {
        new SettingsDescriptor<HarnessSettings>(SettingsKeys.Harness, SettingsScope.PerUser, c => c.Harness, (c, v) => c.Harness = v),
        new SettingsDescriptor<PdpSettings>(SettingsKeys.Pdp, SettingsScope.Global, c => c.Pdp, (c, v) => c.Pdp = v),
    });

    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _sut = CreateSut(new PassThroughHybridCache());
    }

    private SettingsService CreateSut(HybridCache cache) => new(
        _repository,
        _catalog,
        cache,
        Substitute.For<IServiceProvider>(),
        NullLogger<SettingsService>.Instance);

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task GetAsync_ReturnsNull_ForUnknownKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.GetAsync("nope", UserId, ct);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Harness_ReturnsUserValue_WhenRowExists()
    {
        var ct = TestContext.Current.CancellationToken;
        var entity = new SystemSettings
        {
            UserId = UserId,
            Settings = new SettingsContainer { Harness = new HarnessSettings { UserHarnessPrompt = "existing" } }
        };
        _repository.FindAsync(UserId, ct).Returns(entity);

        var result = await _sut.GetAsync(SettingsKeys.Harness, UserId, ct);

        result.Should().NotBeNull();
        result!.Key.Should().Be(SettingsKeys.Harness);
        result.Value.Deserialize<HarnessSettings>(WebOptions)!.UserHarnessPrompt.Should().Be("existing");
    }

    [Fact]
    public async Task GetAsync_Harness_ReturnsDefaults_WithoutSeeding_WhenNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.FindAsync(UserId, ct).Returns((SystemSettings?)null);

        var result = await _sut.GetAsync(SettingsKeys.Harness, UserId, ct);

        result!.Value.Deserialize<HarnessSettings>(WebOptions)!.UserHarnessPrompt.Should().BeEmpty();
        await _repository.DidNotReceive().AddAsync(Arg.Any<SystemSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Harness_PersistsAndEvictsCachedEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        var entity = new SystemSettings { UserId = UserId };
        _repository.FindAsync(UserId, ct).Returns(entity);
        var cache = Substitute.For<HybridCache>();
        var sut = CreateSut(cache);

        var result = await sut.UpdateAsync(
            SettingsKeys.Harness, UserId,
            Payload(new { userHarnessPrompt = "custom", isBuiltInHarnessEnabled = true, isPromptEnhancementEnabled = true }),
            "admin", ct);

        await _repository.Received(1).UpdateAsync(Arg.Any<SystemSettings>(), ct);
        entity.Settings.Harness.UserHarnessPrompt.Should().Be("custom");
        entity.UpdatedBy.Should().Be("admin");
        result.Value.Deserialize<HarnessSettings>(WebOptions)!.UserHarnessPrompt.Should().Be("custom");
        await cache.Received(1).RemoveAsync(RegistrySettingsCacheKeys.Envelope(SettingsKeys.Harness, UserId), ct);
    }

    [Fact]
    public async Task UpdateAsync_Pdp_ForcesGlobalRow_EvenWithUserId()
    {
        var ct = TestContext.Current.CancellationToken;
        var globalRow = new SystemSettings { UserId = null };
        _repository.FindAsync(null, ct).Returns(globalRow);

        await _sut.UpdateAsync(
            SettingsKeys.Pdp, UserId,
            Payload(new { lockdownMode = true, autonomyLevel = 3, semanticIntentCheckEnabled = false, intentCheckModel = "m", rateLimitMaxRequests = 50, rateLimitWindowSeconds = 30 }),
            "admin", ct);

        globalRow.Settings.Pdp.LockdownMode.Should().BeTrue();
        globalRow.Settings.Pdp.AutonomyLevel.Should().Be(3);
        await _repository.Received(1).FindAsync(null, ct);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsKeyNotFound_ForUnknownKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var act = async () => await _sut.UpdateAsync("nope", UserId, Payload(new { }), null, ct);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
