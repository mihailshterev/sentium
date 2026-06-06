using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Watchdog.Application.Settings;
using Sentium.Watchdog.Core.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Watchdog;

// HttpClient / handler lifetimes are intentionally short-lived test fixtures.
#pragma warning disable CA2000

public sealed class RegistryWatchdogSettingsProviderTests
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
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://registry") });

        var sut = new RegistryWatchdogSettingsProvider(factory, BuildCache(), NullLogger<RegistryWatchdogSettingsProvider>.Instance);

        var settings = await sut.GetAsync(CancellationToken.None);

        // Falls back to the class defaults rather than throwing.
        settings.Should().BeEquivalentTo(new WatchdogRuntimeSettings());
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("registry down");
    }
}
