using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sentium.Infrastructure.Storage;

internal sealed class ObjectStoreHealthCheck(IObjectStore store) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await store.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Object storage is unreachable or rejects the credentials.", ex);
        }
    }
}
