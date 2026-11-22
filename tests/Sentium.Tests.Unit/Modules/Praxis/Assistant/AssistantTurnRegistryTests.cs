using Sentium.Praxis.Features.Assistant;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Assistant;

public sealed class AssistantTurnRegistryTests
{
    private const string TurnId = "turn-1";

    private readonly AssistantTurnRegistry _sut = new();
    private readonly Guid _owner = Guid.NewGuid();

    [Fact]
    public void CancelOwned_ByTheOwner_FiresTheStopToken()
    {
        _sut.Track(TurnId, _owner);
        var stop = _sut.StopToken(TurnId);

        Assert.True(_sut.CancelOwned(TurnId, _owner));
        Assert.True(stop.IsCancellationRequested);
    }

    [Fact]
    public void CancelOwned_BySomeoneElse_LeavesTheTurnRunning()
    {
        _sut.Track(TurnId, _owner);

        Assert.False(_sut.CancelOwned(TurnId, Guid.NewGuid()));
        Assert.False(_sut.StopToken(TurnId).IsCancellationRequested);
    }

    [Fact]
    public void ReleasedTurn_CanNoLongerBeStopped()
    {
        _sut.Track(TurnId, _owner);
        _sut.Release(TurnId);

        Assert.False(_sut.CancelOwned(TurnId, _owner));
        Assert.Equal(CancellationToken.None, _sut.StopToken(TurnId));
    }
}
