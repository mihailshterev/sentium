using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Features.Inference;
using Sentium.Vigil.Features.Monitoring;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil.Inference;

public sealed class InferenceHealthProbeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    private static InferenceModelState Model(string id, string status = "loaded", bool failed = false, int deferred = 0)
        => new(InferenceEndpoint.Chat, id, status, failed, RequestsProcessing: 0, RequestsDeferred: deferred, Now);

    [Fact]
    public void NoLoadedModel_IsUnknown()
    {
        var result = InferenceHealthProbe.Evaluate([Model("gemma", status: "unloaded")]);

        Assert.Equal(ServiceStatus.Unknown, result.Status);
        Assert.Equal(InferenceHealthProbe.TargetName, result.ServiceName);
    }

    [Fact]
    public void LoadedModelsWithoutQueue_AreHealthy()
    {
        var result = InferenceHealthProbe.Evaluate([Model("gemma"), Model("embeddinggemma")]);

        Assert.Equal(ServiceStatus.Healthy, result.Status);
        Assert.Contains("2 model(s) loaded", result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void WaitingRequests_AreDegraded()
    {
        var result = InferenceHealthProbe.Evaluate([Model("gemma", deferred: 3)]);

        Assert.Equal(ServiceStatus.Degraded, result.Status);
        Assert.Contains("3 for gemma", result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedLoad_IsDegraded_AndReportedFirst()
    {
        var result = InferenceHealthProbe.Evaluate([Model("qwen", status: "unloaded", failed: true), Model("gemma", deferred: 1)]);

        Assert.Equal(ServiceStatus.Degraded, result.Status);
        Assert.Contains("Failed to load: qwen", result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsync_ReadsTheLiveState()
    {
        var liveState = new InferenceLiveState();
        liveState.Replace(InferenceEndpoint.Chat, [Model("gemma")]);
        var probe = new InferenceHealthProbe(liveState);

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ServiceStatus.Healthy, result.Status);
        Assert.Equal(ComponentKind.Infrastructure, probe.Kind);
    }
}
