using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Api.Controllers;

/// <summary>
/// Manages the global application settings.
/// </summary>
[ApiController]
[Authorize]
[Route("settings")]
public sealed class SettingsController(ISettingsService settingsService) : ControllerBase
{
    /// <summary>
    /// Returns the current global settings.
    /// </summary>
    /// <remarks>
    /// The response is served from the HybridCache L1 on the hot path.
    /// The first call after a cold start or cache eviction fetches from the database and seeds defaults if no row exists.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current settings snapshot.</returns>
    [HttpGet]
    [ProducesResponseType<SettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsDto>> GetSettings(CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        return Ok(settings);
    }

    /// <summary>
    /// Persists updated global settings and triggers cache invalidation across all services.
    /// </summary>
    /// <remarks>
    /// On success the service evicts the shared Redis L2 cache and publishes a NATS
    /// <c>registry.settings.invalidated</c> event. Consuming services (e.g. AgentRuntime) receive
    /// the event and evict their local L1 caches so the new values take effect on the next
    /// agent interaction without a service restart.
    /// </remarks>
    /// <param name="request">The field values to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings snapshot reflecting the applied changes.</returns>
    [HttpPut]
    [ProducesResponseType<SettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsDto>> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Harness.UserHarnessPrompt is { Length: > 16_000 })
        {
            return BadRequest(new { error = "Harness.UserHarnessPrompt may not exceed 16 000 characters." });
        }

        var updatedBy = User.Identity?.Name;
        await settingsService.UpdateAsync(request, updatedBy, ct);

        var updated = await settingsService.GetAsync(ct);
        return Ok(updated);
    }
}
