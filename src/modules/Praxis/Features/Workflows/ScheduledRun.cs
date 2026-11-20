using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// The Schedule an unattended run was started by.
/// </summary>
internal sealed record ScheduledRun(string ScheduleId, string ScheduleName, UnattendedRunPolicy Policy);
