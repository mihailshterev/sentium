using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Security;
using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Backups;

[ApiController]
[AuthorizeSovereign]
[Route($"{VigilRoutes.Prefix}/backups")]
internal sealed class BackupsController(
    IBackupClient client,
    IOptions<BackupMonitorOptions> options,
    TimeProvider time) : ControllerBase
{
    private const string NotConfigured = "Backups are not set up on this installation.";
    private const string Unreachable = "The backup service is unreachable.";

    /// <summary>
    /// The backups at the destination, the last run and the same health verdict Vigil alarms on.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<BackupsOverviewDto>(StatusCodes.Status200OK)]
    public async Task<BackupsOverviewDto> Get(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return new BackupsOverviewDto(false, settings.MaxAgeHours, ServiceStatus.Unknown, NotConfigured, null);
        }

        var status = await client.GetStatusAsync(ct);
        if (status is null)
        {
            return new BackupsOverviewDto(true, settings.MaxAgeHours, ServiceStatus.Unhealthy, Unreachable, null);
        }

        var health = BackupHealthEvaluator.Evaluate(status, time.GetUtcNow(), settings.MaxAge);
        return new BackupsOverviewDto(true, settings.MaxAgeHours, health.Status, health.Details, status);
    }

    /// <summary>
    /// "Back up now": starts a backup and returns at once.
    /// </summary>
    /// <response code="409">A backup is already running.</response>
    /// <response code="503">Backups are not set up, or the backup service is unreachable.</response>
    [HttpPost("runs")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
        {
            return Problem(detail: NotConfigured, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return await client.StartAsync(ct) switch
        {
            BackupStartResult.Started => Accepted(),
            BackupStartResult.AlreadyRunning => Problem(detail: "A backup is already running.", statusCode: StatusCodes.Status409Conflict),
            _ => Problem(detail: Unreachable, statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }
}
