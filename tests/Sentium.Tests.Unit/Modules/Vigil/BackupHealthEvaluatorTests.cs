using Sentium.Vigil.Features.Backups;
using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class BackupHealthEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(36);

    [Fact]
    public void RecentBackup_IsHealthy()
    {
        var health = BackupHealthEvaluator.Evaluate(Status(backups: [Archive(Now.AddHours(-9))]), Now, MaxAge);

        Assert.Equal(ServiceStatus.Healthy, health.Status);
    }

    [Fact]
    public void NewestBackupOlderThanTheLimit_IsUnhealthy()
    {
        var health = BackupHealthEvaluator.Evaluate(
            Status(backups: [Archive(Now.AddHours(-40)), Archive(Now.AddHours(-64))]), Now, MaxAge);

        Assert.Equal(ServiceStatus.Unhealthy, health.Status);
        Assert.Contains("40 hours", health.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupExactlyAtTheLimit_IsStillHealthy()
    {
        var health = BackupHealthEvaluator.Evaluate(Status(backups: [Archive(Now - MaxAge)]), Now, MaxAge);

        Assert.Equal(ServiceStatus.Healthy, health.Status);
    }

    [Fact]
    public void FailedLastRun_IsUnhealthyEvenWithARecentBackup()
    {
        var failed = new BackupRunDto(Now.AddMinutes(-5), Now.AddMinutes(-4), "manual", Succeeded: false, Archive: null, "pg_dump exited with code 1");

        var health = BackupHealthEvaluator.Evaluate(Status(backups: [Archive(Now.AddHours(-2))], lastRun: failed), Now, MaxAge);

        Assert.Equal(ServiceStatus.Unhealthy, health.Status);
        Assert.Contains("pg_dump exited with code 1", health.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void UnmountedDestination_IsUnhealthy()
    {
        var health = BackupHealthEvaluator.Evaluate(
            Status(backups: [], destinationProblem: "The backup destination is not mounted."), Now, MaxAge);

        Assert.Equal(ServiceStatus.Unhealthy, health.Status);
        Assert.Equal("The backup destination is not mounted.", health.Details);
    }

    [Fact]
    public void NoBackupYet_IsHealthyWithinTheLimitOfTheServiceStarting()
    {
        var health = BackupHealthEvaluator.Evaluate(Status(backups: [], startedAt: Now.AddHours(-10)), Now, MaxAge);

        Assert.Equal(ServiceStatus.Healthy, health.Status);
    }

    [Fact]
    public void NoBackupLongAfterTheServiceStarted_IsUnhealthy()
    {
        var health = BackupHealthEvaluator.Evaluate(Status(backups: [], startedAt: Now.AddHours(-37)), Now, MaxAge);

        Assert.Equal(ServiceStatus.Unhealthy, health.Status);
    }

    private static BackupArchiveDto Archive(DateTimeOffset createdAt) => new("sentium.tar", createdAt, 1024, Encrypted: false);

    private static BackupStatusDto Status(
        IReadOnlyList<BackupArchiveDto> backups,
        BackupRunDto? lastRun = null,
        string? destinationProblem = null,
        DateTimeOffset? startedAt = null)
        => new(
            "0.1.0",
            startedAt ?? Now.AddDays(-30),
            DestinationReady: destinationProblem is null,
            destinationProblem,
            Running: false,
            NextRunAt: Now.AddHours(15),
            lastRun,
            backups,
            KeepDaily: 7,
            KeepWeekly: 4,
            EncryptionEnabled: false);
}
