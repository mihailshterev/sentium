using Aspire.Hosting.Testing;
using FluentAssertions;
using Xunit;

namespace Sentium.Tests.AppHost;

public sealed class AppHostTests
{
    [Fact]
    public async Task AppHost_ShouldBuildWithoutErrors_WhenConfigurationIsLoadable()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);
        await using var app = await appHost.BuildAsync(ct);

        // Assert
        appHost.Should().NotBeNull();
        app.Should().NotBeNull();
    }

    [Fact]
    public async Task AppHost_ShouldRegisterExpectedResources_WhenInitialized()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);

        // Act
        await using var app = await appHost.BuildAsync(ct);
        var resourceNames = appHost.Resources.Select(r => r.Name).ToList();

        // Assert
        resourceNames.Should().Contain(Shared.Constants.ServiceNames.Gateway);
        resourceNames.Should().Contain(Shared.Constants.ServiceNames.Sentinel);
        resourceNames.Should().Contain(Shared.Constants.ServiceNames.Watchdog);
        resourceNames.Should().Contain(Shared.Constants.ServiceNames.AgentRuntime);
    }
}
