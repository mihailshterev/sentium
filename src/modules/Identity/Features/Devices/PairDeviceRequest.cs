using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Devices;

internal sealed record PairDeviceRequest(
    [Required, MaxLength(DeviceLimits.PairingCodeInputMax)] string Code,
    [Required, MaxLength(DeviceLimits.NameMax)] string Name);
