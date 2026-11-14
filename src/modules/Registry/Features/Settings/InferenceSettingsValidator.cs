using FluentValidation;

namespace Sentium.Registry.Features.Settings;

internal sealed class InferenceSettingsValidator : AbstractValidator<InferenceSettings>
{
    public InferenceSettingsValidator()
    {
        RuleFor(x => x.DefaultModel)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("DefaultModel must be a non-empty model identifier.");

        RuleFor(x => x.AgentTemperature)
            .InclusiveBetween(0.0f, 1.0f)
            .WithMessage("AgentTemperature must be between 0.0 and 1.0.");
    }
}
