using Microsoft.Extensions.Options;
using Sentium.Backup.Archives;
using Sentium.Backup.Options;
using Sentium.Backup.Status;

namespace Sentium.Backup.Running;

/// <summary>
/// Lets one run at a time happen, whether the schedule or "back up now" started it, and remembers how the last
/// one went. A manual run is detached from the request and stops only when the sidecar shuts down.
/// </summary>
internal sealed class BackupCoordinator(
    BackupRunner runner,
    BackupCatalog catalog,
    IOptions<BackupOptions> options,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<BackupCoordinator> logger) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DateTimeOffset _serviceStartedAt = time.GetUtcNow();
    private volatile BackupRunResult? _lastRun;
    private volatile bool _running;
    private DateTimeOffset? _nextRunAt;

    public DateTimeOffset? NextRunAt
    {
        get => _nextRunAt;
        set => _nextRunAt = value;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        _lastRun = await catalog.ReadLastRunAsync(ct);
    }

    public async Task RunAsync(string trigger, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        await RunHeldAsync(trigger, ct);
    }

    /// <summary>
    /// Starts a run in the background, or returns <see langword="false"/> when one is already in progress.
    /// </summary>
    public bool TryStartManual()
    {
        if (!_gate.Wait(0))
        {
            return false;
        }

        _running = true;
        _ = Task.Run(() => RunHeldAsync(BackupTriggers.Manual, lifetime.ApplicationStopping));
        return true;
    }

    public BackupStatus GetStatus()
    {
        var problem = catalog.FindDestinationProblem();
        var settings = options.Value;

        return new BackupStatus(
            SentiumVersion.CurrentText,
            _serviceStartedAt,
            problem is null,
            problem,
            _running,
            NextRunAt,
            _lastRun,
            problem is null ? catalog.List() : [],
            settings.KeepDaily,
            settings.KeepWeekly,
            settings.IsEncryptionEnabled);
    }

    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// Runs with the gate already taken and releases it. Never throws: the outcome is recorded instead.
    /// </summary>
    private async Task RunHeldAsync(string trigger, CancellationToken ct)
    {
        _running = true;
        var startedAt = time.GetUtcNow();
        try
        {
            var archive = await runner.RunAsync(ct);
            _lastRun = new BackupRunResult(startedAt, time.GetUtcNow(), trigger, Succeeded: true, archive.Name, Error: null);
        }
        catch (Exception ex)
        {
            var error = ex is OperationCanceledException ? "Interrupted by shutdown." : ex.Message;
            logger.LogError(ex, "The {Trigger} backup failed", trigger);
            _lastRun = new BackupRunResult(startedAt, time.GetUtcNow(), trigger, Succeeded: false, Archive: null, error);
        }
        finally
        {
            _running = false;
            _gate.Release();
        }

        await catalog.WriteStatusAsync(GetStatus(), CancellationToken.None);
    }
}
