using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Registry.Core.Settings;
using Sentium.Shared.Constants;

namespace Sentium.Registry.Api.Controllers;

/// <summary>
/// Centralized key-based settings API.
/// </summary>
[ApiController]
[Route("settings")]
public sealed class SettingsController(ISettingsService settingsService, ISettingsCatalog catalog) : ControllerBase
{
    private const string InternalCallerClaimType = "caller-type";
    private const string InternalCallerClaimValue = "internal-system";

    private bool IsSystemCaller => User.HasClaim(InternalCallerClaimType, InternalCallerClaimValue);

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
    /// settings (the internal system caller may pass <paramref name="userId"/> to read another
    /// user's); global keys require the system caller or a Sovereign.
    /// </summary>
    [HttpGet("{key}")]
    [AuthorizeUserOrSystem]
    [ProducesResponseType<SettingsEnvelope>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsEnvelope>> Get(string key, [FromQuery] Guid? userId, CancellationToken ct)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            return NotFound();
        }

        if (descriptor.Scope == SettingsScope.Global && !IsSystemCaller && !IsSovereign)
        {
            return Forbid();
        }

        var effectiveUserId = descriptor.Scope == SettingsScope.Global ? null : IsSystemCaller ? userId : CallerUserId;

        var envelope = await settingsService.GetAsync(key, effectiveUserId, ct);
        return envelope is null ? NotFound() : Ok(envelope);
    }

    /// <summary>
    /// Persists the settings for <paramref name="key"/>. Per-user keys are self-scoped; global keys
    /// require a Sovereign.
    /// </summary>
    [HttpPut("{key}")]
    [AuthorizeUserOrSystem]
    [ProducesResponseType<SettingsEnvelope>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsEnvelope>> Update(string key, [FromBody] JsonElement payload, CancellationToken ct)
    {
        if (!catalog.TryGet(key, out var descriptor))
        {
            return NotFound();
        }

        if (descriptor.Scope == SettingsScope.Global && !IsSystemCaller && !IsSovereign)
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
