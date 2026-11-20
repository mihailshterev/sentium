using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Entities;
using Sentium.Registry.Features.Settings;
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

    private SettingsService CreateSut(HybridCache cache)
    {
        return new(
            _repository,
            _catalog,
            cache,
            Substitute.For<IServiceProvider>(),
            NullLogger<SettingsService>.Instance);
    }

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task GetAsync_ReturnsNull_ForUnknownKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.GetAsync("nope", UserId, ct);
        Assert.Null(result);
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

        Assert.NotNull(result);
        Assert.Equal(SettingsKeys.Harness, result!.Key);
        Assert.Equal("existing", result.Value.Deserialize<HarnessSettings>(WebOptions)!.UserHarnessPrompt);
    }

    [Fact]
    public async Task GetAsync_Harness_ReturnsDefaults_WithoutSeeding_WhenNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.FindAsync(UserId, ct).Returns((SystemSettings?)null);

        var result = await _sut.GetAsync(SettingsKeys.Harness, UserId, ct);

        Assert.Empty(result!.Value.Deserialize<HarnessSettings>(WebOptions)!.UserHarnessPrompt);
        await _repository.DidNotReceive().TryAddAsync(Arg.Any<SystemSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Harness_PersistsAndEvictsCachedEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        var entity = new SystemSettings { UserId = UserId };
        _repository.FindAsync(UserId, ct).Returns(entity);
        _repository.TryUpdateAsync(entity, ct).Returns(true);
        var cache = Substitute.For<HybridCache>();
        var sut = CreateSut(cache);

        var result = await sut.UpdateAsync(
            SettingsKeys.Harness, UserId,
            Payload(new { userHarnessPrompt = "custom", isBuiltInHarnessEnabled = true, isPromptEnhancementEnabled = true }),
            "admin", ct);

        await _repository.Received(1).TryUpdateAsync(Arg.Any<SystemSettings>(), ct);
        Assert.Equal("custom", entity.Settings.Harness.UserHarnessPrompt);
        Assert.Equal("admin", entity.UpdatedBy);
        Assert.Equal("custom", result.Value.Deserialize<HarnessSettings>(WebOptions)!.UserHarnessPrompt);
        await cache.Received(1).RemoveAsync(RegistrySettingsCacheKeys.Envelope(SettingsKeys.Harness, UserId), ct);
    }

    [Fact]
    public async Task UpdateAsync_Pdp_ForcesGlobalRow_EvenWithUserId()
    {
        var ct = TestContext.Current.CancellationToken;
        var globalRow = new SystemSettings { UserId = null };
        _repository.FindAsync(null, ct).Returns(globalRow);
        _repository.TryUpdateAsync(globalRow, ct).Returns(true);

        await _sut.UpdateAsync(
            SettingsKeys.Pdp, UserId,
            Payload(new { lockdownMode = true, autonomyLevel = 3, semanticIntentCheckEnabled = false, intentCheckModel = "m", rateLimitMaxRequests = 50, rateLimitWindowSeconds = 30 }),
            "admin", ct);

        Assert.True(globalRow.Settings.Pdp.LockdownMode);
        Assert.Equal(3, globalRow.Settings.Pdp.AutonomyLevel);
        await _repository.Received(1).FindAsync(null, ct);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsKeyNotFound_ForUnknownKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var act = async () => await _sut.UpdateAsync("nope", UserId, Payload(new { }), null, ct);
        await Assert.ThrowsAnyAsync<KeyNotFoundException>(act);
    }

    [Fact]
    public async Task UpdateAsync_ReReadsAndMerges_WhenAConcurrentUpdateWon()
    {
        var ct = TestContext.Current.CancellationToken;
        var stale = new SystemSettings { UserId = UserId, Version = 1 };
        var fresh = new SystemSettings
        {
            UserId = UserId,
            Version = 2,
            Settings = new SettingsContainer { Harness = new HarnessSettings { IsPromptEnhancementEnabled = true } }
        };
        _repository.FindAsync(UserId, ct).Returns(stale, fresh);
        _repository.TryUpdateAsync(stale, ct).Returns(false);
        _repository.TryUpdateAsync(fresh, ct).Returns(true);

        await _sut.UpdateAsync(SettingsKeys.Harness, UserId, Payload(new { userHarnessPrompt = "mine" }), "admin", ct);

        Assert.Equal("mine", fresh.Settings.Harness.UserHarnessPrompt);
        Assert.True(fresh.Settings.Harness.IsPromptEnhancementEnabled);
    }
}
