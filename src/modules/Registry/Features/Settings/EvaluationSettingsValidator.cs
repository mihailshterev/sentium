using FluentValidation;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Registry.Features.Settings;

internal sealed class EvaluationSettingsValidator : AbstractValidator<EvaluationSettings>
{
    public EvaluationSettingsValidator()
    {
        RuleFor(x => x.JudgeModel)
            .MaximumLength(EvaluationSettings.MaxModelIdLength)
            .Must(m => m is null || !string.IsNullOrWhiteSpace(m))
            .WithMessage("JudgeModel must be a model id, or null for the default model.");
    }
}
