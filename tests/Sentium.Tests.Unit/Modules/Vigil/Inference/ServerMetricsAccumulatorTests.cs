using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Features.Inference;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil.Inference;

public sealed class ServerMetricsAccumulatorTests
{
    private const string Model = "gemma-4-e4b";

    private static readonly DateTimeOffset Minute0 = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly ServerMetricsAccumulator _sut = new();

    private static LlamaServerSnapshot Snapshot(double prompt, double generated, int processing = 0, int deferred = 0)
        => new(prompt, prompt / 100, generated, generated / 10, processing, deferred);

    [Fact]
    public void FirstSnapshot_OnlySetsTheBaseline()
    {
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(5_000, 900), Minute0);

        var minute = Assert.Single(_sut.DrainAll());
        Assert.Equal(0, minute.PromptTokens);
        Assert.Equal(0, minute.GeneratedTokens);
        Assert.Equal(1, minute.Samples);
    }

    [Fact]
    public void LaterSnapshots_AddTheirDeltas_AndKeepThePeakQueue()
    {
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(1_000, 100), Minute0);
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(1_500, 160, processing: 2, deferred: 1), Minute0.AddSeconds(15));
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(1_700, 200, processing: 1), Minute0.AddSeconds(30));

        var minute = Assert.Single(_sut.DrainAll());
        Assert.Equal(700, minute.PromptTokens);
        Assert.Equal(7, minute.PromptSeconds, precision: 6);
        Assert.Equal(100, minute.GeneratedTokens);
        Assert.Equal(10, minute.GenerationSeconds, precision: 6);
        Assert.Equal(2, minute.PeakRequestsProcessing);
        Assert.Equal(1, minute.PeakRequestsDeferred);
        Assert.Equal(3, minute.Samples);
        Assert.Equal(2, minute.BusySamples);
    }

    [Fact]
    public void FallingCounters_MeanARestart_AndCountFromZero()
    {
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(10_000, 2_000), Minute0);
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(300, 50), Minute0.AddSeconds(15));

        var minute = Assert.Single(_sut.DrainAll());
        Assert.Equal(300, minute.PromptTokens);
        Assert.Equal(50, minute.GeneratedTokens);
    }

    [Fact]
    public void AModelSeenUnloaded_CountsEverythingFromItsNextSnapshot()
    {
        _sut.MarkNotLoaded(InferenceEndpoint.Chat, Model);
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(400, 80), Minute0);

        var minute = Assert.Single(_sut.DrainAll());
        Assert.Equal(400, minute.PromptTokens);
        Assert.Equal(80, minute.GeneratedTokens);
    }

    [Fact]
    public void DrainCompleted_KeepsTheCurrentMinute()
    {
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(0, 0), Minute0);
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(10, 10), Minute0.AddMinutes(1));

        var completed = _sut.DrainCompleted(Minute0.AddMinutes(1).AddSeconds(20));

        var minute = Assert.Single(completed);
        Assert.Equal(EpochMinutes.From(Minute0), minute.EpochMinute);
        Assert.Equal(EpochMinutes.From(Minute0.AddMinutes(1)), Assert.Single(_sut.DrainAll()).EpochMinute);
    }

    [Fact]
    public void ModelsOnDifferentServers_AreKeptApart()
    {
        _sut.Observe(InferenceEndpoint.Chat, Model, Snapshot(0, 0), Minute0);
        _sut.Observe(InferenceEndpoint.Embedding, Model, Snapshot(0, 0), Minute0);

        Assert.Equal(2, _sut.DrainAll().Count);
    }
}
