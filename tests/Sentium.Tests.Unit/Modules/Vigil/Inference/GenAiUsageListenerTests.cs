using System.Diagnostics.Metrics;
using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Features.Inference;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil.Inference;

/// <summary>
/// Meter listeners are process-wide, so every test records through a meter with its own caller name and only
/// asserts on that caller.
/// </summary>
public sealed class GenAiUsageListenerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 30, TimeSpan.Zero);

    private readonly GenAiUsageAccumulator _accumulator = new(new SettableClock(Now));
    private readonly GenAiUsageListener _sut;
    private readonly string _caller = $"Test{Guid.NewGuid():N}";
    private readonly Meter _meter;

    public GenAiUsageListenerTests()
    {
        _sut = new GenAiUsageListener(_accumulator);
        _meter = new Meter(GenAiTelemetry.SourceName(_caller));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _meter.Dispose();
    }

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);

    [Fact]
    public async Task RecordsTokensDurationAndFirstChunk_PerCallerModelAndOperation()
    {
        await _sut.StartAsync(CancellationToken.None);
        var tokens = _meter.CreateHistogram<int>("gen_ai.client.token.usage");
        var duration = _meter.CreateHistogram<double>("gen_ai.client.operation.duration");
        var firstChunk = _meter.CreateHistogram<double>("gen_ai.client.operation.time_to_first_chunk");

        tokens.Record(120, Tag("gen_ai.operation.name", "chat"), Tag("gen_ai.request.model", "gemma"), Tag("gen_ai.token.type", "input"));
        tokens.Record(30, Tag("gen_ai.operation.name", "chat"), Tag("gen_ai.request.model", "gemma"), Tag("gen_ai.token.type", "output"));
        duration.Record(2.5, Tag("gen_ai.operation.name", "chat"), Tag("gen_ai.request.model", "gemma"));
        duration.Record(0.5, Tag("gen_ai.operation.name", "chat"), Tag("gen_ai.request.model", "gemma"), Tag("error.type", "HttpRequestException"));
        firstChunk.Record(0.25, Tag("gen_ai.operation.name", "chat"), Tag("gen_ai.request.model", "gemma"));

        var usage = Assert.Single(_accumulator.DrainAll(), u => u.Caller == _caller);
        Assert.Equal("gemma", usage.Model);
        Assert.Equal("chat", usage.Operation);
        Assert.Equal(120, usage.InputTokens);
        Assert.Equal(30, usage.OutputTokens);
        Assert.Equal(2, usage.Requests);
        Assert.Equal(1, usage.Errors);
        Assert.Equal(3.0, usage.DurationSeconds, precision: 6);
        Assert.Equal(0.25, usage.FirstChunkSeconds, precision: 6);
        Assert.Equal(1, usage.FirstChunkCount);
        Assert.Equal(EpochMinutes.From(Now), usage.EpochMinute);
    }

    [Fact]
    public async Task FallsBackToTheResponseModel_WhenTheRequestNamesNone()
    {
        await _sut.StartAsync(CancellationToken.None);
        var duration = _meter.CreateHistogram<double>("gen_ai.client.operation.duration");

        duration.Record(1, Tag("gen_ai.operation.name", "embeddings"), Tag("gen_ai.response.model", "embeddinggemma"));

        var usage = Assert.Single(_accumulator.DrainAll(), u => u.Caller == _caller);
        Assert.Equal("embeddinggemma", usage.Model);
    }

    [Fact]
    public async Task IgnoresOtherInstrumentsAndMeters()
    {
        await _sut.StartAsync(CancellationToken.None);
        using var unrelated = new Meter($"Unrelated.{_caller}");
        unrelated.CreateHistogram<double>("gen_ai.client.operation.duration").Record(1);
        _meter.CreateHistogram<double>("gen_ai.client.operation.something_else").Record(1);

        Assert.DoesNotContain(_accumulator.DrainAll(), u => u.Caller == _caller);
    }

    [Theory]
    [InlineData("Sentium.GenAI.Agents", "Agents")]
    [InlineData("Experimental.Microsoft.Extensions.AI", GenAiUsageListener.OtherCaller)]
    [InlineData("Experimental.Microsoft.Agents.AI", null)]
    public void CallerOf_ReadsTheCallerFromTheMeterName(string meterName, string? expected)
    {
        Assert.Equal(expected, GenAiUsageListener.CallerOf(meterName));
    }

    [Fact]
    public void Accumulator_DrainCompleted_KeepsTheCurrentMinute()
    {
        var clock = new SettableClock(Now);
        var accumulator = new GenAiUsageAccumulator(clock);
        accumulator.RecordDuration("Agents", "gemma", "chat", 1, failed: false);
        clock.Now = Now.AddMinutes(1);
        accumulator.RecordDuration("Agents", "gemma", "chat", 1, failed: false);

        var completed = Assert.Single(accumulator.DrainCompleted(clock.Now));

        Assert.Equal(EpochMinutes.From(Now), completed.EpochMinute);
        Assert.Single(accumulator.DrainAll());
    }
}
