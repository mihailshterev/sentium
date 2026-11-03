using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Registry.Contracts;
using Sentium.Registry.Features.Settings;
using Sentium.Core.Constants;

namespace Sentium.Registry.Controllers;

/// <summary>
/// Centralized key-based settings API.
/// </summary>
[ApiController]
[Authorize]
[Route($"{RegistryRoutes.Prefix}/settings")]
public sealed class SettingsController(ISettingsService settingsService, ISettingsCatalog catalog) : ControllerBase
{
    private bool IsSovereign => RoleClaims.IsInRole(User, SecurityRoles.Sovereign);

    private Guid? CallerUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("nameid");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <summary>
    /// Returns the settings for <paramref name="key"/>. Per-user keys resolve to the caller's own
    /// settings; global keys require a Sovereign. Other modules read settings in-process through
    /// <see cref="ISettingsService"/>, not through this endpoint.
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType<SettingsEnvelope>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsEnvelope>> Get(string key, CancellationToken ct)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            return NotFound();
        }

        if (descriptor.Scope == SettingsScope.Global && !IsSovereign)
        {
            return Forbid();
        }

        var effectiveUserId = descriptor.Scope == SettingsScope.Global ? null : CallerUserId;

        var envelope = await settingsService.GetAsync(key, effectiveUserId, ct);
        return envelope is null ? NotFound() : Ok(envelope);
    }

    /// <summary>
    /// Persists the settings for <paramref name="key"/>. Per-user keys are self-scoped; global keys
    /// require a Sovereign.
    /// </summary>
    [HttpPut("{key}")]
    [ProducesResponseType<SettingsEnvelope>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsEnvelope>> Update(string key, [FromBody] JsonElement payload, CancellationToken ct)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            return NotFound();
        }

        if (descriptor.Scope == SettingsScope.Global && !IsSovereign)
        {
            return Forbid();
        }

        var effectiveUserId = descriptor.Scope == SettingsScope.Global ? null : CallerUserId;

        try
        {
            var envelope = await settingsService.UpdateAsync(key, effectiveUserId, payload, User.Identity?.Name, ct);
            return Ok(envelope);
        }
        catch (ValidationException ex)
        {
            foreach (var failure in ex.Errors)
            {
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }
    }
}
