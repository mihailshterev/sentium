namespace Sentium.Vigil.Features.Devices;

internal sealed record DeviceSample(DateTimeOffset At, double CpuPercent, double MemoryUsedPercent);
