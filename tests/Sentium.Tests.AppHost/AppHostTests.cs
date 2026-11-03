using Aspire.Hosting.Testing;
using FluentAssertions;
using Sentium.Core.Constants;
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

        // Assert - the modular monolith, the isolated sandbox and the shared infrastructure.
        resourceNames.Should().Contain(ServiceNames.Web);
        resourceNames.Should().Contain(ServiceNames.Sandbox);
        resourceNames.Should().Contain(ServiceNames.Frontend);
        resourceNames.Should().Contain(ResourceNames.Postgres);
        resourceNames.Should().Contain(ResourceNames.SentiumDb);
        resourceNames.Should().Contain(ResourceNames.Nats);
        resourceNames.Should().Contain(ResourceNames.Qdrant);
    }
}
