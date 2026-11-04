using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Vigil.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

// The service provider backing the HybridCache is an intentionally short-lived test fixture.
#pragma warning disable CA2000

public sealed class RegistryVigilSettingsProviderTests
{
    private static HybridCache BuildCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenRegistryUnavailable()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetAsync(SettingsKeys.Vigil, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("registry down"));

        var sut = new RegistryVigilSettingsProvider(settingsService, BuildCache(), NullLogger<RegistryVigilSettingsProvider>.Instance);

        var settings = await sut.GetAsync(CancellationToken.None);

        // Falls back to the class defaults rather than throwing.
        settings.Should().BeEquivalentTo(new VigilRuntimeSettings());
    }

    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenKeyIsUnknown()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetAsync(SettingsKeys.Vigil, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((SettingsEnvelope?)null);

        var sut = new RegistryVigilSettingsProvider(settingsService, BuildCache(), NullLogger<RegistryVigilSettingsProvider>.Instance);

        var settings = await sut.GetAsync(CancellationToken.None);

        settings.Should().BeEquivalentTo(new VigilRuntimeSettings());
    }
}
