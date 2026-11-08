namespace Sentium.Sandbox.Mcp;

/// <summary>
/// Lifecycle of the MCP host: reaps containers orphaned by a previous run on startup, stops idle servers, and stops
/// everything on shutdown.
/// </summary>
internal sealed class McpHostService(McpContainerManager manager, ILogger<McpHostService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await manager.ReapOrphansAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not reap orphaned MCP server containers");
        }

        if (!manager.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await manager.StopIdleAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "MCP idle sweep failed");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await manager.DisposeAsync();
    }
}
