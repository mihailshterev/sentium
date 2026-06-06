using System.Diagnostics;
using System.Text.Json;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring.Probes;

public sealed class HttpHealthProbe(IHttpClientFactory httpClientFactory, string target, string clientName) : IHealthProbe
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Target => target;
    public ComponentKind Kind => ComponentKind.Service;

    public async Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync("/health", ct);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(ct);
            return Parse(body, response.IsSuccessStatusCode, (int)response.StatusCode, response.ReasonPhrase, sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, "Probe timed out");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ProbeResult.Create(target, Kind, ServiceStatus.Unhealthy, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    private ServiceHealthStatus Parse(string body, bool isSuccess, int statusCode, string? reason, double latencyMs)
    {
        try
        {
            var report = JsonSerializer.Deserialize<HealthReportDto>(body, JsonOptions);
            if (report is not null && !string.IsNullOrEmpty(report.Status))
            {
                var status = report.Status switch
                {
                    "Healthy" => ServiceStatus.Healthy,
                    "Degraded" => ServiceStatus.Degraded,
                    "Unhealthy" => ServiceStatus.Unhealthy,
                    _ => isSuccess ? ServiceStatus.Healthy : ServiceStatus.Unhealthy
                };

                var checks = report.Entries?
                    .Select(e => new HealthCheckEntry(e.Name, e.Status, e.Description ?? e.Exception, e.DurationMs))
                    .ToList() ?? [];

                var details = status == ServiceStatus.Healthy ? null : SummarizeFailures(checks) ?? $"Reported {report.Status}";
                return ProbeResult.Create(target, Kind, status, latencyMs, details, checks);
            }
        }
        catch (JsonException)
        {
        }

        var fallbackStatus = isSuccess ? ServiceStatus.Healthy : ServiceStatus.Unhealthy;
        var fallbackDetails = isSuccess ? null : $"HTTP {statusCode} {reason}";
        return ProbeResult.Create(target, Kind, fallbackStatus, latencyMs, fallbackDetails);
    }

    private static string? SummarizeFailures(IReadOnlyList<HealthCheckEntry> checks)
    {
        var failing = checks.Where(c => c.Status != "Healthy").Select(c => c.Name).ToList();
        return failing.Count == 0 ? null : $"Failing checks: {string.Join(", ", failing)}";
    }

    private sealed record HealthReportDto(string Status, double TotalDurationMs, List<HealthEntryDto>? Entries);

    private sealed record HealthEntryDto(string Name, string Status, string? Description, double DurationMs, string? Exception);
}
