using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
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
        Assert.NotNull(appHost);
        Assert.NotNull(app);
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
        Assert.Contains(ServiceNames.Web, resourceNames);
        Assert.Contains(ServiceNames.Sandbox, resourceNames);
        Assert.Contains(ServiceNames.Frontend, resourceNames);
        Assert.Contains(ResourceNames.Postgres, resourceNames);
        Assert.Contains(ResourceNames.SentiumDb, resourceNames);
        Assert.All(new[] { ResourceNames.Llm, ResourceNames.Embeddings, ResourceNames.ModelsInit }, expected => Assert.Contains(expected, resourceNames));
        Assert.Contains(ResourceNames.Searxng, resourceNames);
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
        Assert.Matches("^[0-9a-f]{64}$", image.SHA256);
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
        var env = configuration.EnvironmentVariables.ToDictionary();

        // Assert - MCP containers may use only the internal network that SearXNG shares.
        Assert.Equal("sentium-egress", Assert.Contains(EnvConfig.Keys.SandboxMcp.AllowedNetwork, env));
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
        Assert.Equal(3, images.Count);
        // Every image must be pinned by digest.
        Assert.All(images, i => Assert.Matches("^[0-9a-f]{64}$", i.Image.SHA256));
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
        var env = configuration.EnvironmentVariables.ToDictionary();

        // Assert - chat goes to the llm router, embeddings to their own server, both with the shared API key.
        Assert.Contains($"{{{ResourceNames.Llm}.bindings.http.url}}", Assert.Contains(EnvConfig.Keys.Inference.ChatEndpoint, env), StringComparison.Ordinal);
        Assert.Contains($"{{{ResourceNames.Embeddings}.bindings.http.url}}", Assert.Contains(EnvConfig.Keys.Inference.EmbeddingEndpoint, env), StringComparison.Ordinal);
        Assert.Contains("llm-api-key", Assert.Contains(EnvConfig.Keys.Inference.ApiKey, env), StringComparison.Ordinal);
        Assert.Equal(AIModels.Gemma4_E4B, Assert.Contains(EnvConfig.Keys.Inference.DefaultModel, env));
        Assert.Equal(AIModels.EmbeddingGemma2, Assert.Contains(EnvConfig.Keys.Inference.EmbeddingModelName, env));
    }

    [Fact]
    public async Task Backup_ShouldReachTheDatabaseAndBlobs_AndBeKnownToWeb()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sentium_AppHost>(ct);
        var context = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish);
        var resources = appHost.Resources.OfType<IResourceWithEnvironment>().ToList();

        // Act
        var backupEnv = (await ExecutionConfigurationBuilder.Create(resources.Single(r => r.Name == ResourceNames.Backup))
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance, ct)).EnvironmentVariables.ToDictionary();
        var webEnv = (await ExecutionConfigurationBuilder.Create(resources.Single(r => r.Name == ServiceNames.Web))
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance, ct)).EnvironmentVariables.ToDictionary();

        // Assert - the sidecar dumps sentiumdb and copies the blobs; Sentium.Web probes it and offers "back up now".
        Assert.Contains($"ConnectionStrings__{ResourceNames.SentiumDb}", backupEnv);
        Assert.Contains($"ConnectionStrings__{ResourceNames.WorkspaceBlobs}", backupEnv);
        Assert.Equal($"Endpoint={{{ResourceNames.Storage}.bindings.http.url}};AccessKey={{storage-access-key.value}};SecretKey={{storage-secret-key.value}}", appHost.Resources.OfType<IResourceWithConnectionString>().Single(r => r.Name == ResourceNames.WorkspaceBlobs)
            .ConnectionStringExpression.ValueExpression);
        Assert.Contains(EnvConfig.Keys.InternalApiKey, backupEnv);
        Assert.Contains($"{{{ResourceNames.Backup}.bindings.http.url}}", Assert.Contains(EnvConfig.Keys.Backup.Endpoint, webEnv), StringComparison.Ordinal);
    }
}
