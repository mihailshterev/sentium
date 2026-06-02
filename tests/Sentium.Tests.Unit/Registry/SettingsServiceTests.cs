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
    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();
    private readonly SpyEventBus _eventBus = new();
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _sut = new SettingsService(
            _repository,
            new PassThroughHybridCache(),
            _eventBus,
            NullLogger<SettingsService>.Instance);
    }

    private static UpdateSettingsRequest MakeUpdateRequest(bool promptEnhancement = true) =>
        new(new UpdateHarnessSettingsRequest("custom prompt", true, promptEnhancement));

    [Fact]
    public async Task GetAsync_ReturnsExistingSettings_WhenRowExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entity = new SystemSettings
        {
            Settings = new SettingsContainer
            {
                Harness = new HarnessSettings { UserHarnessPrompt = "existing", IsBuiltInHarnessEnabled = true }
            }
        };
        _repository.FindAsync(ct).Returns(entity);

        // Act
        var result = await _sut.GetAsync(ct);

        // Assert
        result.Harness.UserHarnessPrompt.Should().Be("existing");
    }

    [Fact]
    public async Task GetAsync_SeedsDefaults_WhenNoRowExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _repository.FindAsync(ct).Returns((SystemSettings?)null);

        // Act
        var result = await _sut.GetAsync(ct);

        // Assert
        result.Should().NotBeNull();
        await _repository.Received(1).AddAsync(Arg.Any<SystemSettings>(), ct);
    }

    [Fact]
    public async Task UpdateAsync_PersistsNewValues_WhenRowExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var entity = new SystemSettings();
        _repository.FindAsync(ct).Returns(entity);
        var request = MakeUpdateRequest();

        // Act
        await _sut.UpdateAsync(request, "admin", ct);

        // Assert
        await _repository.Received(1).SaveAsync(ct);
        entity.Settings.Harness.UserHarnessPrompt.Should().Be("custom prompt");
        entity.UpdatedBy.Should().Be("admin");
    }

    [Fact]
    public async Task UpdateAsync_SeedsRow_ThenPersists_WhenNoRowExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _repository.FindAsync(ct).Returns((SystemSettings?)null);

        // Act
        await _sut.UpdateAsync(MakeUpdateRequest(), null, ct);

        // Assert
        await _repository.Received(1).AddAsync(Arg.Any<SystemSettings>(), ct);
        await _repository.Received(1).SaveAsync(ct);
    }

    [Fact]
    public async Task UpdateAsync_PublishesNatsEvent_OnSuccess()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _repository.FindAsync(ct).Returns(new SystemSettings());

        // Act
        await _sut.UpdateAsync(MakeUpdateRequest(), null, ct);

        // Assert
        _eventBus.PublishedSubjects.Should().ContainSingle(s => s == NatsSubjects.SettingsInvalidated);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsArgumentNull_WhenRequestNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var act = async () => await _sut.UpdateAsync(null!, null, ct);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
