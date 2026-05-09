using Sentium.AgentRuntime.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Manages the singleton system-wide settings.
/// </summary>
[ApiController]
[Authorize]
[Route("system-settings")]
public sealed class SystemSettingsController(ISystemSettingsService systemSettingsService) : ControllerBase
{
    /// <summary>
    /// Returns the current system settings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await systemSettingsService.GetAsync(ct);
        return Ok(settings);
    }

    /// <summary>
    /// Persists updated system settings.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSystemSettingsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserHarnessPrompt is { Length: > 16_000 })
        {
            return BadRequest(new { error = "User harness prompt may not exceed 16 000 characters." });
        }

        var updatedBy = User.Identity?.Name;
        await systemSettingsService.UpdateAsync(request, updatedBy, ct);

        var updated = await systemSettingsService.GetAsync(ct);
        return Ok(updated);
    }
}
