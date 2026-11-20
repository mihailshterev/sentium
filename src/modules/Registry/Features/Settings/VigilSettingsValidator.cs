using FluentValidation;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Cross-field rules only; per-field bounds are the contract's DataAnnotations.
/// </summary>
internal sealed class VigilSettingsValidator : AbstractValidator<VigilSettings>
{
    public VigilSettingsValidator()
    {
        RuleFor(x => x.ProbeTimeoutSeconds)
            .LessThanOrEqualTo(x => x.PollIntervalSeconds)
            .WithMessage("ProbeTimeoutSeconds must not exceed PollIntervalSeconds.");
    }
}
