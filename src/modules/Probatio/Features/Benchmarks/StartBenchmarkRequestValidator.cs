using FluentValidation;
using Sentium.Probatio.Common;

namespace Sentium.Probatio.Features.Benchmarks;

internal sealed class StartBenchmarkRequestValidator : AbstractValidator<StartBenchmarkRequest>
{
    public StartBenchmarkRequestValidator()
    {
        RuleFor(x => x.SuiteId).NotEmpty();
        RuleFor(x => x.SuiteVersion).GreaterThanOrEqualTo(1).When(x => x.SuiteVersion.HasValue);
        RuleFor(x => x.AgentName).NotEmpty().MaximumLength(ProbatioLimits.AgentNameMax);
        RuleFor(x => x.Repetitions).InclusiveBetween(1, ProbatioLimits.RepetitionsMax);
        RuleFor(x => x.Models)
            .NotNull()
            .Must(m => m is not null && m.Count is >= ProbatioLimits.BenchmarkModelsMin and <= ProbatioLimits.BenchmarkModelsMax)
            .WithMessage($"Pick {ProbatioLimits.BenchmarkModelsMin} to {ProbatioLimits.BenchmarkModelsMax} models.")
            .Must(m => m is null || m.Select(x => x?.Trim()).Distinct(StringComparer.Ordinal).Count() == m.Count)
            .WithMessage("Each model can be picked only once.");
        RuleForEach(x => x.Models).NotEmpty().MaximumLength(ProbatioLimits.ModelNameMax);
    }
}
