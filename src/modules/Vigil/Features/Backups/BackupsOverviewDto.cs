using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Backups;

/// <summary>
/// The portal's Backups panel. <see cref="Status"/> is <see langword="null"/> when backups are not set up
/// (<see cref="Configured"/> is false) or the sidecar cannot be reached.
/// </summary>
internal sealed record BackupsOverviewDto(
    bool Configured,
    int MaxAgeHours,
    ServiceStatus Health,
    string? HealthDetails,
    BackupStatusDto? Status);
