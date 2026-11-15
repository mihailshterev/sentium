using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Results;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity.Features.Devices;

/// <summary>
/// Pairing and managing Devices. Everything here is for the signed-in portal user except <see cref="Pair"/>, which
/// the tray app calls before it has a credential, and <see cref="UnpairCurrent"/>, which it calls with one.
/// </summary>
[ApiController]
[Route($"{IdentityRoutes.Prefix}/devices")]
internal sealed class DevicesController(IDeviceService devices, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Creates a pairing code for the signed-in user, replacing any earlier one.
    /// </summary>
    /// <response code="409">The user already has the maximum number of Devices.</response>
    [HttpPost("pairing-codes")]
    [Authorize]
    [ProducesResponseType(typeof(PairingCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePairingCode(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return (await devices.CreatePairingCodeAsync(userId, ct)).ToActionResult(this);
    }

    /// <summary>
    /// Exchanges a pairing code for a Device credential.
    /// </summary>
    /// <response code="400">The code is unknown, expired or already used.</response>
    /// <response code="409">The owner of the code already has the maximum number of Devices.</response>
    [HttpPost("pair")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(PairDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pair([FromBody] PairDeviceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appVersion = DeviceUserAgent.ParseAppVersion(Request.Headers[HeaderNames.UserAgent].ToString());

        return (await devices.PairAsync(request.Code, request.Name, appVersion, ct)).ToActionResult(this);
    }

    /// <summary>
    /// Lists the caller's Devices.
    /// </summary>
    /// <param name="all">Sovereigns only: list every user's Devices.</param>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] bool all = false, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (all && !currentUser.IsSovereign)
        {
            return Forbid();
        }

        return Ok(await devices.ListAsync(all ? null : userId, ct));
    }

    /// <summary>
    /// Removes a Device; its token stops working. Users remove their own, Sovereigns anyone's.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var removed = await devices.RemoveAsync(id, currentUser.IsSovereign ? null : userId, ct);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// The tray app unpairs itself.
    /// </summary>
    [HttpDelete("current")]
    [AuthorizeDevice]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnpairCurrent(CancellationToken ct)
    {
        if (User.GetDeviceId() is not { } deviceId)
        {
            return Unauthorized();
        }

        await devices.RemoveAsync(deviceId, ownerUserId: null, ct);
        return NoContent();
    }
}
