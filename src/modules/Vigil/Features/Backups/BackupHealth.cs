using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Backups;

internal sealed record BackupHealth(ServiceStatus Status, string? Details);
