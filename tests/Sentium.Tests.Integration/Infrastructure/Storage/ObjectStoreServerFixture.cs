using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Storage;
using Xunit;

namespace Sentium.Tests.Integration.Infrastructure.Storage;

/// <summary>
/// The object store server, image and flags as the deployments run it (docker-compose.yml, StorageConfig), so the
/// contract tests exercise the real server rather than a stand-in. Keep the three in step.
/// </summary>
public sealed class ObjectStoreServerFixture : IAsyncLifetime
{
    public const string AccessKey = "contract-access-key";
    public const string SecretKey = "contract-secret-key-0123456789abcdef";

    private const string Image = "chrislusf/seaweedfs:4.54@sha256:4b42c212543a0acd4c0e675215ecdcaf62a186ef98def76c7e12701871222d15";
    private const int S3Port = 8333;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithCommand("mini", "-dir=/data", "-ip=127.0.0.1", "-ip.bind=0.0.0.0", "-master.telemetry=false", "-admin.ui=false", "-webdav=false", "-s3.port.iceberg=0", "-s3.port.lance=0")
        .WithEnvironment("AWS_ACCESS_KEY_ID", AccessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", SecretKey)
        .WithPortBinding(S3Port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(S3Port).ForPath("/healthz")))
        .Build();

    private IHost? _host;

    public IObjectStore Store => _host!.Services.GetRequiredService<IObjectStore>();

    public IServiceProvider Services => _host!.Services;

    public string ConnectionString(string accessKey = AccessKey, string secretKey = SecretKey)
    {
        return $"Endpoint=http://{_container.Hostname}:{_container.GetMappedPublicPort(S3Port)};AccessKey={accessKey};SecretKey={secretKey}";
    }

    /// <summary>
    /// Builds a host the way the services do (<see cref="ObjectStoreExtensions.AddSentiumObjectStore"/>).
    /// </summary>
    public IHost BuildHost(string connectionString)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:storage"] = connectionString,
        });
        builder.AddSentiumObjectStore("storage");
        return builder.Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _host = BuildHost(ConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        _host?.Dispose();
        await _container.DisposeAsync();
    }
}
