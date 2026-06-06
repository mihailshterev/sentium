using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Security;
using Sentium.Shared.Constants;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace Sentium.Tests.Integration.Common;

public class ServiceWebApplicationFactory<TProgram>(
    string dbResourceName,
    bool withRedis = false) : WebApplicationFactory<TProgram>, IAsyncLifetime where TProgram : class
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7.0").Build();

    public async ValueTask InitializeAsync()
    {
        var startTasks = withRedis
            ? new[] { _dbContainer.StartAsync(), _redisContainer.StartAsync() }
            : new[] { _dbContainer.StartAsync() };
        await Task.WhenAll(startTasks);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting($"ConnectionStrings:{dbResourceName}", _dbContainer.GetConnectionString());
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Nats}", "nats://localhost:4222");
        builder.UseSetting($"ConnectionStrings:{ResourceNames.Seq}", "http://localhost:5341");

        if (withRedis)
        {
            builder.UseSetting($"ConnectionStrings:{ResourceNames.Redis}", _redisContainer.GetConnectionString());
        }

        builder.UseSetting("Identity:Authority", "http://localhost:5000");
        builder.UseSetting("Identity:GatewayBffSecret", "test-secret-123");

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

            services.AddAuthorizationBuilder()
                .AddPolicy(Policies.Sovereign, policy =>
                    policy.AddAuthenticationSchemes(TestAuthHandler.AuthenticationScheme)
                          .RequireAuthenticatedUser()
                          .RequireAssertion(ctx => RoleClaims.IsInRole(ctx.User, SecurityRoles.Sovereign)));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
        if (withRedis)
        {
            await _redisContainer.DisposeAsync();
        }
    }
}
