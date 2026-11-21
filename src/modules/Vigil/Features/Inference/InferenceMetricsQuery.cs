using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Data;
using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Reads the stored minutes back in buckets of the range's step. Buckets are aligned to whole multiples of the
/// step since the epoch, so a refresh moves the window without reshuffling which minutes share a bucket.
/// </summary>
internal sealed class InferenceMetricsQuery(VigilDbContext db, InferenceLiveState liveState, TimeProvider timeProvider) : IInferenceMetricsQuery
{
    private const int MaxLoadEvents = 50;
    private const int Decimals = 1;
    private const int SecondsDecimals = 3;
    private const double Percent = 100;

    public async Task<InferenceOverviewDto> GetOverviewAsync(InferenceRange range, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(range);

        var now = timeProvider.GetUtcNow();
        var toMinute = EpochMinutes.From(now) + 1;
        long step = range.StepMinutes;
        var fromMinute = (toMinute - range.LengthMinutes) / step * step;
        var from = EpochMinutes.ToTime(fromMinute);

        var series = await LoadSeriesAsync(fromMinute, toMinute, step, ct);
        var usage = await LoadUsageAsync(fromMinute, toMinute, ct);
        var loads = await LoadModelLoadsAsync(from, ct);
        var models = liveState.Snapshot().Select(ToDto).ToList();

        return new InferenceOverviewDto(range.Name, range.StepMinutes, from, now, models, series, usage, loads);
    }

    private async Task<IReadOnlyList<InferenceSeriesDto>> LoadSeriesAsync(long fromMinute, long toMinute, long step, CancellationToken ct)
    {
        var buckets = await db.InferenceServerMinutes
            .AsNoTracking()
            .Where(r => r.EpochMinute >= fromMinute && r.EpochMinute < toMinute)
            .GroupBy(r => new { r.Server, r.Model, Slot = r.EpochMinute / step })
            .Select(g => new ServerBucket(
                g.Key.Server,
                g.Key.Model,
                g.Key.Slot,
                g.Sum(r => r.PromptTokens),
                g.Sum(r => r.PromptSeconds),
                g.Sum(r => r.GeneratedTokens),
                g.Sum(r => r.GenerationSeconds),
                g.Max(r => r.PeakRequestsProcessing),
                g.Max(r => r.PeakRequestsDeferred),
                g.Sum(r => r.Samples),
                g.Sum(r => r.BusySamples)))
            .ToListAsync(ct);

        return buckets
            .GroupBy(b => (b.Server, b.Model))
            .OrderBy(g => g.Key.Server)
            .ThenBy(g => g.Key.Model, StringComparer.Ordinal)
            .Select(g => new InferenceSeriesDto(
                g.Key.Server.ToString(),
                g.Key.Model,
                g.OrderBy(b => b.Slot).Select(b => ToPoint(b, step)).ToList()))
            .ToList();
    }

    private async Task<IReadOnlyList<InferenceUsageDto>> LoadUsageAsync(long fromMinute, long toMinute, CancellationToken ct)
    {
        var totals = await db.InferenceUsageMinutes
            .AsNoTracking()
            .Where(r => r.EpochMinute >= fromMinute && r.EpochMinute < toMinute)
            .GroupBy(r => new { r.Caller, r.Model, r.Operation })
            .Select(g => new UsageTotals(
                g.Key.Caller,
                g.Key.Model,
                g.Key.Operation,
                g.Sum(r => r.Requests),
                g.Sum(r => r.Errors),
                g.Sum(r => r.InputTokens),
                g.Sum(r => r.OutputTokens),
                g.Sum(r => r.DurationSeconds),
                g.Sum(r => r.FirstChunkSeconds),
                g.Sum(r => r.FirstChunkCount)))
            .ToListAsync(ct);

        return totals
            .OrderByDescending(t => t.InputTokens + t.OutputTokens)
            .ThenBy(t => t.Caller, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    private async Task<IReadOnlyList<ModelLoadEventDto>> LoadModelLoadsAsync(DateTimeOffset from, CancellationToken ct)
    {
        var events = await db.ModelLoadEvents
            .AsNoTracking()
            .Where(e => e.CompletedAt >= from)
            .OrderByDescending(e => e.CompletedAt)
            .Take(MaxLoadEvents)
            .ToListAsync(ct);

        return events.Select(ToDto).ToList();
    }

    private static InferencePointDto ToPoint(ServerBucket bucket, long step)
    {
        return new InferencePointDto(
            EpochMinutes.ToTime(bucket.Slot * step),
            Rate(bucket.PromptTokens, bucket.PromptSeconds),
            Rate(bucket.GeneratedTokens, bucket.GenerationSeconds),
            bucket.PromptTokens,
            bucket.GeneratedTokens,
            bucket.PeakRequestsProcessing,
            bucket.PeakRequestsDeferred,
            bucket.Samples == 0 ? 0 : Math.Round(bucket.BusySamples * Percent / bucket.Samples, Decimals));
    }

    private static InferenceUsageDto ToDto(UsageTotals totals)
    {
        return new InferenceUsageDto(
            totals.Caller,
            totals.Model,
            totals.Operation,
            totals.Requests,
            totals.Errors,
            totals.InputTokens,
            totals.OutputTokens,
            Average(totals.DurationSeconds, totals.Requests),
            Average(totals.FirstChunkSeconds, totals.FirstChunkCount));
    }

    private static ModelLoadEventDto ToDto(ModelLoadEvent loadEvent)
    {
        return new ModelLoadEventDto(
            loadEvent.Server.ToString(),
            loadEvent.Model,
            loadEvent.StartedAt,
            loadEvent.CompletedAt,
            Math.Round((loadEvent.CompletedAt - loadEvent.StartedAt).TotalSeconds, Decimals),
            loadEvent.Succeeded);
    }

    private static InferenceModelStateDto ToDto(InferenceModelState state)
    {
        return new InferenceModelStateDto(
            state.Server.ToString(),
            state.Model,
            state.Status,
            state.LoadFailed,
            state.RequestsProcessing,
            state.RequestsDeferred,
            state.UpdatedAt);
    }

    private static double? Rate(long tokens, double seconds)
    {
        return seconds > 0 ? Math.Round(tokens / seconds, Decimals) : null;
    }

    private static double? Average(double sum, long count)
    {
        return count > 0 ? Math.Round(sum / count, SecondsDecimals) : null;
    }

    private sealed record ServerBucket(
        InferenceEndpoint Server,
        string Model,
        long Slot,
        long PromptTokens,
        double PromptSeconds,
        long GeneratedTokens,
        double GenerationSeconds,
        int PeakRequestsProcessing,
        int PeakRequestsDeferred,
        int Samples,
        int BusySamples);

    private sealed record UsageTotals(
        string Caller,
        string Model,
        string Operation,
        long Requests,
        long Errors,
        long InputTokens,
        long OutputTokens,
        double DurationSeconds,
        double FirstChunkSeconds,
        long FirstChunkCount);
}
