using Sentium.Nuntius.Features.Devices;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Nuntius;

public sealed class DeviceStreamRegistryTests
{
    private readonly DeviceStreamRegistry _sut = new();

    [Fact]
    public void RemovingADevice_ClosesAllItsStreams_AndNobodyElses()
    {
        var removed = Guid.NewGuid();
        using var first = _sut.Open(removed, CancellationToken.None);
        using var second = _sut.Open(removed, CancellationToken.None);
        using var other = _sut.Open(Guid.NewGuid(), CancellationToken.None);

        _sut.OnDeviceRemoved(removed, Guid.NewGuid());

        Assert.True(first.Token.IsCancellationRequested);
        Assert.True(second.Token.IsCancellationRequested);
        Assert.False(other.Token.IsCancellationRequested);
    }

    [Fact]
    public void Lease_FollowsTheRequest()
    {
        using var request = new CancellationTokenSource();
        using var lease = _sut.Open(Guid.NewGuid(), request.Token);

        request.Cancel();

        Assert.True(lease.Token.IsCancellationRequested);
    }

    [Fact]
    public void RemovingADevice_AfterItsStreamEnded_DoesNothing()
    {
        var device = Guid.NewGuid();
        _sut.Open(device, CancellationToken.None).Dispose();

        var act = () => _sut.OnDeviceRemoved(device, Guid.NewGuid());

        act();
    }
}
