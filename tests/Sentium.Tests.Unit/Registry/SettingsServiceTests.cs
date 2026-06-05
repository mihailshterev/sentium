using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Registry.Application.Settings;
using Sentium.Registry.Core.Entities;
using Sentium.Registry.Core.Settings;
using Sentium.Shared.Constants;
using Sentium.Tests.Unit.Common;
using Xunit;

namespace Sentium.Tests.Unit.Registry;

public sealed class SettingsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();
    private readonly SpyEventBus _eventBus = new();
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        var catalog = new SettingsCatalog(new ISettingsDescriptor[]
        {
            new SettingsDescriptor<HarnessSettings>(SettingsKeys.Harness, SettingsScope.PerUser, c => c.Harness, (c, v) => c.Harness = v),
            new SettingsDescriptor<PdpSettings>(SettingsKeys.Pdp, SettingsScope.Global, c => c.Pdp, (c, v) => c.Pdp = v),
        });

        _sut = new SettingsService(
            _repository,
            catalog,
            new PassThroughHybridCache(),
            _eventBus,
            Substitute.For<IServiceProvider>(),
            NullLogger<SettingsService>.Instance);
    }

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
        result.Value.Should().BeOfType<HarnessSettings>().Which.UserHarnessPrompt.Should().Be("existing");
    }

    [Fact]
    public async Task GetAsync_Harness_ReturnsDefaults_WithoutSeeding_WhenNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.FindAsync(UserId, ct).Returns((SystemSettings?)null);

        var result = await _sut.GetAsync(SettingsKeys.Harness, UserId, ct);

        result!.Value.Should().BeOfType<HarnessSettings>().Which.UserHarnessPrompt.Should().BeEmpty();
        await _repository.DidNotReceive().AddAsync(Arg.Any<SystemSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Harness_PersistsAndPublishes()
    {
        var ct = TestContext.Current.CancellationToken;
        var entity = new SystemSettings { UserId = UserId };
        _repository.FindAsync(UserId, ct).Returns(entity);

        var result = await _sut.UpdateAsync(
            SettingsKeys.Harness, UserId,
            Payload(new { userHarnessPrompt = "custom", isBuiltInHarnessEnabled = true, isPromptEnhancementEnabled = true }),
            "admin", ct);

        await _repository.Received(1).UpdateAsync(Arg.Any<SystemSettings>(), ct);
        entity.Settings.Harness.UserHarnessPrompt.Should().Be("custom");
        entity.UpdatedBy.Should().Be("admin");
        result.Value.Should().BeOfType<HarnessSettings>().Which.UserHarnessPrompt.Should().Be("custom");
        _eventBus.PublishedSubjects.Should().ContainSingle(s => s == NatsSubjects.SettingsInvalidated);
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
