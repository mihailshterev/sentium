using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Core.Constants;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// Boots the whole modular monolith (Sentium.Web) against a real PostgreSQL container; every module
/// migrates its own schema on startup. Background workers are removed.
/// </summary>
public class MonolithWebApplicationFactory : WebApplicationFactory<Sentium.Web.Program>, IAsyncLifetime
{
    /// <summary>
    /// Pre-shared key the out-of-process Sandbox uses to call internal endpoints (Sentinel's evaluate).
    /// </summary>
    public const string InternalApiKey = "integration-test-internal-api-key";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7.0").Build();

    /// <summary>
    /// The Testcontainers PostgreSQL connection string (available once the fixture is initialized).
    /// </summary>
    public string DatabaseConnectionString => _dbContainer.GetConnectionString();

    /// <summary>
    /// The Testcontainers Redis connection string (available once the fixture is initialized).
    /// </summary>
    public string RedisConnectionString => _redisContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.UseSetting($"ConnectionStrings:{ResourceNames.SentiumDb}", _dbContainer.GetConnectionString());
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Redis}", _redisContainer.GetConnectionString());

        builder.UseSetting($"ConnectionStrings:{ResourceNames.WorkspaceBlobs}", "UseDevelopmentStorage=true");
        builder.UseSetting("InternalApi:ApiKey", InternalApiKey);

        builder.ConfigureTestServices(services =>
        {
            var workersToRemove = services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Namespace?.StartsWith("Sentium.", StringComparison.Ordinal) == true)
                .ToList();

            foreach (var descriptor in workersToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, _ => { });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_dbContainer.DisposeAsync().AsTask(), _redisContainer.DisposeAsync().AsTask());
        GC.SuppressFinalize(this);
    }
}
