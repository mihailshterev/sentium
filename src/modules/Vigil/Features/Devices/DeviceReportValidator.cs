using FluentValidation;

namespace Sentium.Vigil.Features.Devices;

internal sealed class DeviceReportValidator : AbstractValidator<DeviceReport>
{
    private const int MachineNameMax = 64;
    private const int OsDescriptionMax = 128;
    private const int AppVersionMax = 32;
    private const int DiskNameMax = 32;
    private const int MaxDisks = 32;

    public DeviceReportValidator()
    {
        RuleFor(x => x.MachineName).NotEmpty().MaximumLength(MachineNameMax);
        RuleFor(x => x.OsDescription).NotNull().MaximumLength(OsDescriptionMax);
        RuleFor(x => x.AppVersion).NotNull().MaximumLength(AppVersionMax);
        RuleFor(x => x.UptimeSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CpuPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.MemoryTotalBytes).GreaterThan(0);
        RuleFor(x => x.MemoryAvailableBytes).InclusiveBetween(0, long.MaxValue);
        RuleFor(x => x.MemoryAvailableBytes)
            .LessThanOrEqualTo(x => x.MemoryTotalBytes)
            .WithMessage("MemoryAvailableBytes must not exceed MemoryTotalBytes.");

        RuleFor(x => x.Disks).NotNull();
        RuleFor(x => x.Disks.Count)
            .LessThanOrEqualTo(MaxDisks)
            .When(x => x.Disks is not null)
            .WithMessage($"A Report carries at most {MaxDisks} disks.");

        RuleForEach(x => x.Disks).ChildRules(disk =>
        {
            disk.RuleFor(d => d.Name).NotEmpty().MaximumLength(DiskNameMax);
            disk.RuleFor(d => d.TotalBytes).GreaterThanOrEqualTo(0);
            disk.RuleFor(d => d.AvailableBytes).GreaterThanOrEqualTo(0);
            disk.RuleFor(d => d.AvailableBytes)
                .LessThanOrEqualTo(d => d.TotalBytes)
                .WithMessage("AvailableBytes must not exceed TotalBytes.");
        });
    }
}
