using Microsoft.AspNetCore.Mvc;
using Sentium.Backup.Running;
using Sentium.Backup.Status;
using Sentium.Infrastructure.Security;

namespace Sentium.Backup.Controllers;

/// <summary>
/// Read by Vigil (status probe and the portal's Backups panel) and Sentium.Web's "back up now", on the internal
/// network only.
/// </summary>
[ApiController]
[AuthorizeSystem]
[Route("api/backup")]
internal sealed class BackupController(BackupCoordinator coordinator) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<BackupStatus>(StatusCodes.Status200OK)]
    public ActionResult<BackupStatus> GetStatus() => coordinator.GetStatus();

    /// <summary>
    /// Starts a backup now and returns at once; the status shows it running and then its result.
    /// </summary>
    /// <response code="409">A backup is already running.</response>
    [HttpPost("runs")]
    [ProducesResponseType<BackupStatus>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public IActionResult Start()
    {
        if (!coordinator.TryStartManual())
        {
            return Problem(detail: "A backup is already running.", statusCode: StatusCodes.Status409Conflict);
        }

        return Accepted(coordinator.GetStatus());
    }
}
