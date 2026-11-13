using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Net;
using Sentium.Infrastructure.Security;
using Sentium.Mcp.Data;
using Sentium.Mcp.Features.Runtime;
using Sentium.Mcp.Features.Servers;
using Sentium.Praxis.Contracts;

namespace Sentium.Mcp;

/// <summary>
/// Model Context Protocol integration: the registry of MCP servers (schema <c>mcp</c>), the hash-pinned approved tool
/// catalogue, and the client that talks to HTTP and Sandbox-hosted stdio servers. It plugs into Praxis as an
/// <see cref="IAgentToolSource"/>, so agents only get MCP tools that their tool list names explicitly.
/// </summary>
public static class McpModuleExtensions
{
    internal const string ModuleName = "mcp";

    public static IHostApplicationBuilder AddMcpModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<McpDbContext>(McpDbContext.Schema);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IMcpSettingsProvider, RegistryMcpSettingsProvider>();
        services.AddSingleton<McpSecretProtector>();
        services.AddSingleton<McpClientManager>();
        services.AddSingleton<McpToolCatalog>();
        services.AddSingleton<IMcpToolCatalog>(sp => sp.GetRequiredService<McpToolCatalog>());
        services.AddSingleton<IAgentToolSource>(sp => sp.GetRequiredService<McpToolCatalog>());
        services.AddScoped<IMcpServerRepository, McpServerRepository>();
        services.AddScoped<IMcpServerService, McpServerService>();
        services.AddHostedService<McpBackgroundService>();

        // Direct MCP servers: local network only, enforced on the resolved address at connect time (DNS-rebinding safe),
        // no proxy, no redirects. Streams stay open between calls, so no overall request timeout - calls are bounded by
        // the per-call timeout in the MCP settings.
        services.AddHttpClient(McpClientManager.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => NetworkAddressPolicy.CreateHandler(NetworkAddressScope.PrivateOnly))
            .AddLongRunningResilienceHandler(
                totalTimeout: TimeSpan.FromMinutes(15),
                attemptTimeout: TimeSpan.FromMinutes(15),
                retries: 0);

        // Stdio servers: the Sandbox host (internal network, internal API key) runs and speaks to them.
        services.TryAddTransient<InternalApiKeyDelegatingHandler>();
        services.AddHttpClient<ISandboxMcpHostClient, SandboxMcpHostClient>(client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Sandbox}");
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddHttpMessageHandler<InternalApiKeyDelegatingHandler>()
        .AddLongRunningResilienceHandler(
            totalTimeout: TimeSpan.FromMinutes(11),
            attemptTimeout: TimeSpan.FromMinutes(10),
            retries: 0);

        services.AddHealthChecks().AddDbContextCheck<McpDbContext>("mcp-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeMcpModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<McpDbContext>();
    }
}
