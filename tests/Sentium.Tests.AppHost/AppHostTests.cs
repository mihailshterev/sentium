using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    }

    [Fact]
    public async Task Web_ShouldReceiveTheInferenceEndpointsAndModels()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);
        var web = appHost.Resources.OfType<IResourceWithEnvironment>().Single(r => r.Name == ServiceNames.Web);

        // Act - publish mode renders endpoint references as expressions, so no containers are started.
        var configuration = await ExecutionConfigurationBuilder.Create(web)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish), NullLogger.Instance, ct);
        var env = configuration.EnvironmentVariables;

        // Assert - both inference endpoints point at the inference resource; models come from the AppHost.
        env.Should().ContainKey(EnvConfig.Keys.Inference.ChatEndpoint)
            .WhoseValue.Should().Contain(ResourceNames.Ollama);
        env.Should().ContainKey(EnvConfig.Keys.Inference.EmbeddingEndpoint)
            .WhoseValue.Should().Contain(ResourceNames.Ollama);
        env.Should().Contain(EnvConfig.Keys.Inference.DefaultModel, AIModels.Gemma4_E4B);
        env.Should().Contain(EnvConfig.Keys.Inference.EmbeddingModelName, AIModels.EmbeddingGemma2_740M);
    }
}
