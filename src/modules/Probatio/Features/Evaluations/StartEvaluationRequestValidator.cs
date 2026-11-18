using FluentValidation;
using Sentium.Probatio.Common;

namespace Sentium.Probatio.Features.Evaluations;

internal sealed class StartEvaluationRequestValidator : AbstractValidator<StartEvaluationRequest>
{
    public StartEvaluationRequestValidator()
    {
        RuleFor(x => x.SuiteId).NotEmpty();
        RuleFor(x => x.SuiteVersion).GreaterThanOrEqualTo(1).When(x => x.SuiteVersion.HasValue);
        RuleFor(x => x.AgentName).NotEmpty().MaximumLength(ProbatioLimits.AgentNameMax);
        RuleFor(x => x.Model).MaximumLength(ProbatioLimits.ModelNameMax);
        RuleFor(x => x.Repetitions).InclusiveBetween(1, ProbatioLimits.RepetitionsMax);
    }
}
