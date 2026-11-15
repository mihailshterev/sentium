namespace Sentium.Vigil.Features.Devices;

internal sealed record DeviceDiskReport(string Name, long TotalBytes, long AvailableBytes)
{
    public double UsedPercent => TotalBytes <= 0 ? 0 : (TotalBytes - AvailableBytes) * 100d / TotalBytes;
}
