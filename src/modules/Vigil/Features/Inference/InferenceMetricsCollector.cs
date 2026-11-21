using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Entities;
using Sentium.Vigil.Features.Settings;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Samples both inference servers every Vigil poll interval: the model list (load tracking and live state) and
/// each loaded model's metrics. Completed minutes, together with the GenAI usage the listener collected, are
/// written once their minute is over; on shutdown the partial minute is written too.
/// </summary>
internal sealed class InferenceMetricsCollector(
    ILlamaMetricsClient client,
    GenAiUsageAccumulator usage,
    InferenceLiveState liveState,
    IVigilSettingsProvider settingsProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<InferenceMetricsCollector> logger) : BackgroundService
{
    private static readonly InferenceEndpoint[] Servers = [InferenceEndpoint.Chat, InferenceEndpoint.Embedding];
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    // Used when the Registry settings cannot be read, so a failing cycle does not spin.
    private static readonly TimeSpan FallbackInterval = TimeSpan.FromSeconds(15);

    private readonly ServerMetricsAccumulator _serverMetrics = new();
    private readonly ModelLoadTracker _loadTracker = new();
    private readonly List<ModelLoadEvent> _pendingLoadEvents = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var interval = await RunCycleAsync(stoppingToken);
                await Task.Delay(interval, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        await FlushOnShutdownAsync();
    }

    /// <returns>How long to wait before the next cycle.</returns>
    private async Task<TimeSpan> RunCycleAsync(CancellationToken ct)
    {
        var interval = FallbackInterval;

        try
        {
            var settings = await settingsProvider.GetAsync(ct);
            interval = TimeSpan.FromSeconds(settings.PollIntervalSeconds);

            await CollectAsync(ct);

            var now = timeProvider.GetUtcNow();
            await FlushAsync(_serverMetrics.DrainCompleted(now), usage.DrainCompleted(now), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Inference metrics collection failed; retrying at the next interval");
        }

        return interval;
    }

    private async Task FlushOnShutdownAsync()
    {
        using var timeout = new CancellationTokenSource(ShutdownFlushTimeout);

        try
        {
            await FlushAsync(_serverMetrics.DrainAll(), usage.DrainAll(), timeout.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Timed out storing the last inference metrics on shutdown");
        }
    }

    private async Task CollectAsync(CancellationToken ct)
    {
        foreach (var server in Servers)
        {
            var models = await client.ListModelsAsync(server, ct);
            if (models is null)
            {
                liveState.Clear(server);
                continue;
            }

            var now = timeProvider.GetUtcNow();
            _pendingLoadEvents.AddRange(_loadTracker.Observe(server, models, now));

            var states = new List<InferenceModelState>(models.Count);
            foreach (var model in models)
            {
                states.Add(await SampleAsync(server, model, now, ct));
            }

            liveState.Replace(server, states);
        }
    }

    private async Task<InferenceModelState> SampleAsync(InferenceEndpoint server, ServedModel model, DateTimeOffset now, CancellationToken ct)
    {
        if (!model.IsLoaded)
        {
            _serverMetrics.MarkNotLoaded(server, model.Id);
            return new InferenceModelState(server, model.Id, model.Status, model.Failed, 0, 0, now);
        }

        var snapshot = await client.GetSnapshotAsync(server, model, ct);
        if (snapshot is null)
        {
            return new InferenceModelState(server, model.Id, model.Status, model.Failed, 0, 0, now);
        }

        _serverMetrics.Observe(server, model.Id, snapshot, now);

        return new InferenceModelState(server, model.Id, model.Status, model.Failed, snapshot.RequestsProcessing, snapshot.RequestsDeferred, now);
    }

    /// <summary>
    /// A failed write is logged and its minutes are dropped: metrics history is best effort and must not pile up in
    /// memory while the database is away.
    /// </summary>
    private async Task FlushAsync(IReadOnlyList<InferenceServerMinute> serverMinutes, IReadOnlyList<InferenceUsageMinute> usageMinutes, CancellationToken ct)
    {
        var loadEvents = _pendingLoadEvents.ToList();
        _pendingLoadEvents.Clear();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InferenceMetricsWriter>().WriteAsync(serverMinutes, usageMinutes, loadEvents, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not store {Count} inference metric rows; they are dropped", serverMinutes.Count + usageMinutes.Count + loadEvents.Count);
        }
    }
}
