using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Sentium.Core.Constants;

namespace Sentium.Infrastructure.Diagnostics;

/// <summary>
/// The one health check for the shared PostgreSQL database. Every module keeps its own schema in that database, so a
/// per-module check would only report the same outage once per module.
/// </summary>
public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string Name = "database";
    public const string Tag = "database";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(configuration.GetConnectionString(ResourceNames.SentiumDb));
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context?.Registration.FailureStatus ?? HealthStatus.Unhealthy, "The database cannot be reached.", ex);
        }
    }
}
