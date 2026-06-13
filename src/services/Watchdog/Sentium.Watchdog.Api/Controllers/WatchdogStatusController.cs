using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Watchdog.Application.Monitoring;
using Sentium.Watchdog.Core.Monitoring;
using Sentium.Watchdog.Core.Settings;

namespace Sentium.Watchdog.Api.Controllers;

[ApiController]
[Authorize]
[Route("status")]
public sealed class WatchdogStatusController(
    IServiceHealthStateStore stateStore,
    IIncidentStore incidentStore,
    IWatchdogSettingsProvider settingsProvider) : ControllerBase
{
    /// <summary>
    /// Returns the latest health for every monitored target (services and infrastructure).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceHealthStatus>>(StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(stateStore.GetAll());

    /// <summary>
    /// Aggregate roll-up across all targets plus the open-incident count.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType<SystemHealthOverview>(StatusCodes.Status200OK)]
    public IActionResult GetOverview() => Ok(HealthAggregator.BuildOverview(stateStore.GetAll(), incidentStore.OpenCount));

    /// <summary>
    /// Returns one target's current health plus its recent sample history.
    /// </summary>
    [HttpGet("{serviceName}")]
    [ProducesResponseType<ServiceHealthDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string serviceName, CancellationToken ct)
    {
        var status = stateStore.Get(serviceName);
        if (status is null)
        {
            return NotFound();
        }

        var settings = await settingsProvider.GetAsync(ct);
        var samples = stateStore.GetSamples(serviceName, settings.SampleHistorySize);
        return Ok(new ServiceHealthDetail(status, samples));
    }
}

public sealed record ServiceHealthDetail(ServiceHealthStatus Status, IReadOnlyList<HealthSample> Samples);
