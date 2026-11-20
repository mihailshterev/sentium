using System.Globalization;
using Quartz;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <param name="RunId">The run's history row; <see langword="null"/> when it could not be persisted.</param>
internal sealed record ScheduleLastRun(Guid? RunId, string Status, DateTimeOffset At)
{
    public static ScheduleLastRun? Read(JobDataMap data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Find(ScheduledJobData.LastRunStatus) is not { Length: > 0 } status
            || !DateTimeOffset.TryParse(data.Find(ScheduledJobData.LastRunAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
        {
            return null;
        }

        return new ScheduleLastRun(Guid.TryParse(data.Find(ScheduledJobData.LastRunId), out var runId) ? runId : null, status, at);
    }

    public static void Write(JobDataMap data, UnattendedRunCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(completion);

        data[ScheduledJobData.LastRunId] = completion.RunId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;
        data[ScheduledJobData.LastRunStatus] = completion.Status.ToString();
        data[ScheduledJobData.LastRunAt] = completion.CompletedAt.ToString("o", CultureInfo.InvariantCulture);
    }
}
