using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Security;
using Sentium.Vigil.Features.Settings;

namespace Sentium.Vigil.Features.Devices;

[ApiController]
[Route($"{VigilRoutes.Prefix}/devices")]
internal sealed class DevicesController(
    IDeviceReportProcessor reportProcessor,
    IDeviceStateStore stateStore,
    IDeviceDirectory deviceDirectory,
    IVigilSettingsProvider settingsProvider,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Takes a Report from a paired Device.
    /// </summary>
    [HttpPost("report")]
    [AuthorizeDevice]
    [ProducesResponseType(typeof(DeviceReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Report([FromBody] DeviceReport report, CancellationToken ct)
    {
        if (User.GetDeviceId() is not { } deviceId || currentUser.UserId is not { } ownerUserId)
        {
            return Unauthorized();
        }

        return Ok(await reportProcessor.ProcessAsync(deviceId, ownerUserId, report, ct));
    }

    /// <summary>
    /// The Devices the caller may see, with their online state and latest Report: their own, or every Device for a
    /// Sovereign.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var visible = await deviceDirectory.ListAsync(currentUser.IsSovereign ? null : userId, ct);

        return Ok(visible.Select(d => DeviceStatusDto.From(d, stateStore.Get(d.Id))).ToList());
    }

    /// <summary>
    /// One Device plus its recent CPU and memory samples.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(DeviceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var visible = await deviceDirectory.ListAsync(currentUser.IsSovereign ? null : userId, ct);
        var device = visible.FirstOrDefault(d => d.Id == id);
        if (device is null)
        {
            return NotFound();
        }

        var settings = await settingsProvider.GetAsync(ct);

        return Ok(new DeviceDetailDto(
            DeviceStatusDto.From(device, stateStore.Get(id)),
            stateStore.GetSamples(id, settings.SampleHistorySize)));
    }
}
