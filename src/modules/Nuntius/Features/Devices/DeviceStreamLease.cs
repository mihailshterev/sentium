namespace Sentium.Nuntius.Features.Devices;

internal sealed class DeviceStreamLease(CancellationTokenSource cts, Action close) : IDisposable
{
    public CancellationToken Token { get; } = cts.Token;

    public void Dispose() => close();
}
