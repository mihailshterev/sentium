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
        resourceNames.Should().Contain([ResourceNames.Llm, ResourceNames.Embeddings, ResourceNames.ModelsInit]);
    }

    [Fact]
    public async Task Searxng_ShouldBePinnedByDigest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);

        // Act
        var image = appHost.Resources.Single(r => r.Name == ResourceNames.Searxng)
            .Annotations.OfType<ContainerImageAnnotation>().Single();

        // Assert - the search backend is a supply-chain input too.
        image.SHA256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Sandbox_ShouldAllowTheWebSearchImageAndTheInternalEgressNetwork()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);
        var sandbox = appHost.Resources.OfType<IResourceWithEnvironment>().Single(r => r.Name == ServiceNames.Sandbox);

        // Act
        var configuration = await ExecutionConfigurationBuilder.Create(sandbox)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish), NullLogger.Instance, ct);
        var env = configuration.EnvironmentVariables;

        // Assert - MCP containers may use only the internal network that SearXNG shares.
        env.Should().Contain(EnvConfig.Keys.SandboxMcp.AllowedNetwork, "sentium-egress");
    }

    [Fact]
    public async Task InferenceContainers_ShouldBePinnedByDigest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);

        // Act
        var images = appHost.Resources
            .Where(r => r.Name is ResourceNames.Llm or ResourceNames.Embeddings or ResourceNames.ModelsInit)
            .Select(r => (r.Name, Image: r.Annotations.OfType<ContainerImageAnnotation>().Single()))
            .ToList();

        // Assert - model-serving images are supply-chain inputs: a tag alone could be re-pointed.
        images.Should().HaveCount(3);
        images.Should().AllSatisfy(i => i.Image.SHA256.Should().MatchRegex("^[0-9a-f]{64}$", $"{i.Name} must be pinned by digest"));
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

        // Assert - chat goes to the llm router, embeddings to their own server, both with the shared API key.
        env.Should().ContainKey(EnvConfig.Keys.Inference.ChatEndpoint)
            .WhoseValue.Should().Contain($"{{{ResourceNames.Llm}.bindings.http.url}}");
        env.Should().ContainKey(EnvConfig.Keys.Inference.EmbeddingEndpoint)
            .WhoseValue.Should().Contain($"{{{ResourceNames.Embeddings}.bindings.http.url}}");
        env.Should().ContainKey(EnvConfig.Keys.Inference.ApiKey)
            .WhoseValue.Should().Contain("llm-api-key");
        env.Should().Contain(EnvConfig.Keys.Inference.DefaultModel, AIModels.Gemma4_E4B);
        env.Should().Contain(EnvConfig.Keys.Inference.EmbeddingModelName, AIModels.EmbeddingGemma2);
    }
}
