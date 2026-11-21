using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Features.Inference;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil.Inference;

public sealed class ModelLoadTrackerTests
{
    private const string Model = "qwen";

    private static readonly DateTimeOffset Start = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly ModelLoadTracker _sut = new();

    private static ServedModel Routed(string status, bool failed = false) => new(Model, status, IsRouted: true, Failed: failed);

    [Fact]
    public void LoadingThenLoaded_ReportsASuccessfulLoad_FromFirstSeenLoading()
    {
        Assert.Empty(_sut.Observe(InferenceEndpoint.Chat, [Routed("loading")], Start));
        Assert.Empty(_sut.Observe(InferenceEndpoint.Chat, [Routed("loading")], Start.AddSeconds(15)));

        var loaded = Assert.Single(_sut.Observe(InferenceEndpoint.Chat, [Routed("loaded")], Start.AddSeconds(30)));

        Assert.True(loaded.Succeeded);
        Assert.Equal(Model, loaded.Model);
        Assert.Equal(Start, loaded.StartedAt);
        Assert.Equal(Start.AddSeconds(30), loaded.CompletedAt);
        Assert.Empty(_sut.Observe(InferenceEndpoint.Chat, [Routed("loaded")], Start.AddSeconds(45)));
    }

    [Fact]
    public void LoadingThenFailedUnloaded_ReportsAFailedLoad()
    {
        _sut.Observe(InferenceEndpoint.Chat, [Routed("loading")], Start);

        var failed = Assert.Single(_sut.Observe(InferenceEndpoint.Chat, [Routed("unloaded", failed: true)], Start.AddSeconds(15)));

        Assert.False(failed.Succeeded);
    }

    [Fact]
    public void AModelNeverSeenLoading_ReportsNothing()
    {
        Assert.Empty(_sut.Observe(InferenceEndpoint.Chat, [Routed("unloaded")], Start));
        Assert.Empty(_sut.Observe(InferenceEndpoint.Chat, [Routed("loaded")], Start.AddSeconds(15)));
    }

    [Fact]
    public void AModelThatLeavesTheListWhileLoading_IsForgotten()
    {
        _sut.Observe(InferenceEndpoint.Chat, [Routed("loading")], Start);
        _sut.Observe(InferenceEndpoint.Chat, [], Start.AddSeconds(15));

        Assert.Empty(_sut.Observe(InferenceEndpoint.Chat, [Routed("loaded")], Start.AddSeconds(30)));
    }
}
