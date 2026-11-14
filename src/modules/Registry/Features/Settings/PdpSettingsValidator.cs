using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class PdpSettingsValidator : AbstractValidator<PdpSettings>
{
    public PdpSettingsValidator()
    {
        RuleFor(x => x.AutonomyLevel)
            .InclusiveBetween(1, 10)
            .WithMessage("AutonomyLevel must be between 1 and 10.");

        RuleFor(x => x.RateLimitMaxRequests)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RateLimitMaxRequests must be at least 1.");

        RuleFor(x => x.RateLimitWindowSeconds)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RateLimitWindowSeconds must be at least 1.");

        RuleFor(x => x.IntentCheckModel)
            .MaximumLength(256)
            .WithMessage("IntentCheckModel may not exceed 256 characters.");
    }
}
