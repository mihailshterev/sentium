using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Constants;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace Sentium.Tests.Integration.Common;

public class SentiumWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime where TProgram : class
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7.0").Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("Identity:Authority", "http://localhost:5000");
        builder.UseSetting("Identity:GatewayBffSecret", "test-secret-123");

        builder.UseSetting($"ConnectionStrings:{ResourceNames.AgentRuntimeDb}", _dbContainer.GetConnectionString());
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Redis}", _redisContainer.GetConnectionString());

        builder.UseSetting($"ConnectionStrings:{ResourceNames.Nats}", "nats://localhost:4222");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Qdrant}", "Endpoint=http://localhost:6334");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.WorkspaceBlobs}", "UseDevelopmentStorage=true");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Seq}", "http://localhost:5341");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventBus>();
            services.AddSingleton(Substitute.For<IEventBus>());

            var workersToRemove = services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Namespace?.StartsWith("Sentium.") == true)
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
    }
}
