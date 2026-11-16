using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    private readonly StreamRelay _relay = new(new ConfigurationBuilder().Build(), NullLogger<StreamRelay>.Instance);
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

        _relay.IsVisibleTo("stream-1", Owner).Should().BeTrue();
        _relay.IsVisibleTo("stream-1", Stranger).Should().BeFalse();
        _relay.IsVisibleTo("unknown", Stranger).Should().BeTrue();
    }

    [Fact]
    public void CancelOwned_StopsOnlyTheOwnersRun()
    {
        _registry.Register("stream-1", Owner, _run);

        _registry.CancelOwned("stream-1", Stranger).Should().BeFalse();
        _run.IsCancellationRequested.Should().BeFalse();

        _registry.CancelOwned("stream-1", Owner).Should().BeTrue();
        _run.IsCancellationRequested.Should().BeTrue();
    }
}
