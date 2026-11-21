using System.Diagnostics.Metrics;
using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Listens in-process to the Microsoft.Extensions.AI GenAI instruments (token usage, operation duration, time to
/// first chunk) and feeds them to <see cref="GenAiUsageAccumulator"/>. The caller is read from the meter name (see
/// <see cref="GenAiTelemetry"/>); a pipeline that names no source counts as <see cref="OtherCaller"/>. Runs
/// alongside the OpenTelemetry exporter, which keeps its own listener.
/// </summary>
internal sealed class GenAiUsageListener(GenAiUsageAccumulator accumulator) : IHostedService, IDisposable
{
    public const string OtherCaller = "Other";

    private const string TokenUsage = "gen_ai.client.token.usage";
    private const string OperationDuration = "gen_ai.client.operation.duration";
    private const string TimeToFirstChunk = "gen_ai.client.operation.time_to_first_chunk";

    private const string OperationTag = "gen_ai.operation.name";
    private const string RequestModelTag = "gen_ai.request.model";
    private const string ResponseModelTag = "gen_ai.response.model";
    private const string TokenTypeTag = "gen_ai.token.type";
    private const string ErrorTypeTag = "error.type";
    private const string InputTokenType = "input";
    private const string UnknownValue = "unknown";

    private static readonly string CallerPrefix = GenAiTelemetry.SourcePrefix + ".";

    private MeterListener? _listener;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener = new MeterListener { InstrumentPublished = OnInstrumentPublished };
        _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _listener = null;
    }

    internal static string? CallerOf(string meterName)
    {
        if (meterName.StartsWith(CallerPrefix, StringComparison.Ordinal))
        {
            return meterName[CallerPrefix.Length..];
        }

        return meterName == GenAiTelemetry.DefaultSourceName ? OtherCaller : null;
    }

    private static void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if ((instrument.Name is TokenUsage or OperationDuration or TimeToFirstChunk) && CallerOf(instrument.Meter.Name) is not null)
        {
            listener.EnableMeasurementEvents(instrument);
        }
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (CallerOf(instrument.Meter.Name) is not { } caller)
        {
            return;
        }

        var measurement = MeasurementTags.Read(tags);
        var model = measurement.RequestModel ?? measurement.ResponseModel ?? UnknownValue;
        var operation = measurement.Operation ?? UnknownValue;

        switch (instrument.Name)
        {
            case TokenUsage:
                accumulator.RecordTokens(caller, model, operation, measurement.TokenType == InputTokenType, (long)value);
                break;
            case OperationDuration:
                accumulator.RecordDuration(caller, model, operation, value, measurement.Failed);
                break;
            case TimeToFirstChunk:
                accumulator.RecordFirstChunk(caller, model, operation, value);
                break;
        }
    }

    private readonly record struct MeasurementTags(string? Operation, string? RequestModel, string? ResponseModel, string? TokenType, bool Failed)
    {
        public static MeasurementTags Read(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var result = default(MeasurementTags);

            foreach (var (key, value) in tags)
            {
                result = key switch
                {
                    OperationTag => result with { Operation = value as string },
                    RequestModelTag => result with { RequestModel = value as string },
                    ResponseModelTag => result with { ResponseModel = value as string },
                    TokenTypeTag => result with { TokenType = value as string },
                    ErrorTypeTag => result with { Failed = value is not null },
                    _ => result,
                };
            }

            return result;
        }
    }
}
