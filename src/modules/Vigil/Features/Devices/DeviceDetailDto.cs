namespace Sentium.Vigil.Features.Devices;

internal sealed record DeviceDetailDto(DeviceStatusDto Status, IReadOnlyList<DeviceSample> Samples);
