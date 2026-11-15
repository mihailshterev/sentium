namespace Sentium.Vigil.Features.Devices;

/// <summary>
/// The latest Report of a Device as Vigil holds it in memory.
/// </summary>
/// <param name="IsOnline">
/// <see langword="false"/> once three Reports in a row are overdue. Offline is a normal state (the PC sleeps or is
/// switched off), never an Incident.
/// </param>
internal sealed record DeviceSnapshot(
    Guid DeviceId,
    Guid OwnerUserId,
    DeviceReport Report,
    DateTimeOffset ReportedAt,
    bool IsOnline);
