using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Messaging;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// Boots the whole modular monolith (Sentium.Web) against a real PostgreSQL container; every module
/// migrates its own schema on startup. Background workers and NATS are stubbed out.
/// </summary>
public class MonolithWebApplicationFactory : WebApplicationFactory<Sentium.Web.Program>, IAsyncLifetime
{
    /// <summary>
    /// Pre-shared key the out-of-process Sandbox uses to call internal endpoints (Sentinel's evaluate).
    /// </summary>
    public const string InternalApiKey = "integration-test-internal-api-key";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7.0").Build();

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

        builder.UseSetting($"ConnectionStrings:{ResourceNames.Nats}", "nats://localhost:4222");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Qdrant}", "Endpoint=http://localhost:6334");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.WorkspaceBlobs}", "UseDevelopmentStorage=true");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Seq}", "http://localhost:5341");
        builder.UseSetting("InternalApi:ApiKey", InternalApiKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventBus>();
            services.AddSingleton(Substitute.For<IEventBus>());

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
