using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Praxis.Features.Orchestration;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Workflows;

/// <summary>
/// A run's stream and its stop button belong to the user who started it.
/// </summary>
public sealed class RunOwnershipTests : IDisposable
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private readonly StreamRelay _relay = new(Options.Create(new StreamingOptions { TextCoalesceWindow = TimeSpan.Zero }), NullLogger<StreamRelay>.Instance);
    private readonly WorkflowExecutionRegistry _registry = new();
    private readonly CancellationTokenSource _run = new();

    public void Dispose()
    {
        _relay.Dispose();
        _run.Dispose();
    }

    [Fact]
    public void AClaimedStream_IsVisibleOnlyToItsOwner()
    {
        _relay.Claim("stream-1", Owner);

        Assert.True(_relay.IsVisibleTo("stream-1", Owner));
        Assert.False(_relay.IsVisibleTo("stream-1", Stranger));
        Assert.True(_relay.IsVisibleTo("unknown", Stranger));
    }

    [Fact]
    public void CancelOwned_StopsOnlyTheOwnersRun()
    {
        _registry.Register("stream-1", Owner, _run);

        Assert.False(_registry.CancelOwned("stream-1", Stranger));
        Assert.False(_run.IsCancellationRequested);

        Assert.True(_registry.CancelOwned("stream-1", Owner));
        Assert.True(_run.IsCancellationRequested);
    }
}
