using Docker.DotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sentium.Sandbox.Infrastructure.Docker;

internal sealed class DockerHealthCheck(IDockerClient dockerClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await dockerClient.System.PingAsync(cts.Token);
            return HealthCheckResult.Healthy("Docker daemon is reachable.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Docker daemon ping timed out.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Docker daemon is unreachable.", ex);
        }
    }
}
