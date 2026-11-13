using Sentium.Mcp.Features.Servers;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Loads the MCP tool catalog once the host is up (retrying while the database is not ready yet) and then closes MCP
/// connections that sat idle - or all of them while the MCP kill switch is off.
/// </summary>
public sealed class McpBackgroundService(
    IMcpToolCatalog catalog,
    McpClientManager clients,
    IMcpSettingsProvider settings,
    ILogger<McpBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan LoadRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadCatalogAsync(stoppingToken);

        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var runtime = await settings.GetAsync(stoppingToken);
                if (runtime.Enabled)
                {
                    await clients.CloseIdleAsync(TimeSpan.FromMinutes(runtime.IdleTimeoutMinutes));
                }
                else
                {
                    await clients.CloseAllAsync();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "MCP connection sweep failed");
            }
        }
    }

    private async Task LoadCatalogAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await catalog.EnsureLoadedAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not load the MCP tool catalog; retrying in {Delay}", LoadRetryDelay);
                await Task.Delay(LoadRetryDelay, stoppingToken);
            }
        }
    }
}
