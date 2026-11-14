using FluentValidation;

namespace Sentium.Registry.Features.Settings;

public sealed class VigilSettingsValidator : AbstractValidator<VigilSettings>
{
    public VigilSettingsValidator()
    {
        RuleFor(x => x.PollIntervalSeconds)
            .InclusiveBetween(5, 3_600)
            .WithMessage("PollIntervalSeconds must be between 5 and 3600.");

        RuleFor(x => x.ProbeTimeoutSeconds)
            .InclusiveBetween(1, 60)
            .WithMessage("ProbeTimeoutSeconds must be between 1 and 60.");

        RuleFor(x => x.ProbeTimeoutSeconds)
            .LessThanOrEqualTo(x => x.PollIntervalSeconds)
            .WithMessage("ProbeTimeoutSeconds must not exceed PollIntervalSeconds.");

        RuleFor(x => x.DegradedLatencyMs)
            .InclusiveBetween(1, 60_000)
            .WithMessage("DegradedLatencyMs must be between 1 and 60000.");

        RuleFor(x => x.ConsecutiveFailuresToOpenIncident)
            .InclusiveBetween(1, 100)
            .WithMessage("ConsecutiveFailuresToOpenIncident must be between 1 and 100.");

        RuleFor(x => x.SampleHistorySize)
            .InclusiveBetween(10, 200)
            .WithMessage("SampleHistorySize must be between 10 and 200.");
    }
}
