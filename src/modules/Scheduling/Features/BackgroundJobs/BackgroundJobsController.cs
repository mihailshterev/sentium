using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.BackgroundWork;
using Sentium.Infrastructure.Security;

namespace Sentium.Scheduling.Features.BackgroundJobs;

/// <summary>
/// Every background worker hosted in Sentium.Web and the work running right now, across all users, so Sovereign only.
/// </summary>
[ApiController]
[AuthorizeSovereign]
[Route($"{SchedulingRoutes.Prefix}/scheduler/background-jobs")]
internal sealed class BackgroundJobsController(
    IEnumerable<IHostedService> hostedServices,
    IEnumerable<IBackgroundActivitySource> activitySources,
    ILogger<BackgroundJobsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<BackgroundJobsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var running = new List<BackgroundActivity>();

        foreach (var source in activitySources)
        {
            try
            {
                running.AddRange(await source.GetActivitiesAsync(ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Background activity source {Source} failed.", source.GetType().Name);
            }
        }

        return Ok(new BackgroundJobsResponse(BackgroundServiceInspector.Describe(hostedServices), running));
    }
}
