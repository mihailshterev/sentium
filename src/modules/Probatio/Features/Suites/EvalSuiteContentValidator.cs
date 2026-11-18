using FluentValidation;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Common;

namespace Sentium.Probatio.Features.Suites;

internal sealed class EvalSuiteContentValidator : AbstractValidator<IEvalSuiteContent>
{
    public EvalSuiteContentValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("A suite needs a name.")
            .MaximumLength(ProbatioLimits.SuiteNameMax);
        RuleFor(x => x.Description).MaximumLength(ProbatioLimits.SuiteDescriptionMax);
        RuleFor(x => x.Cases)
            .Must(c => c is null || c.Count <= ProbatioLimits.CasesPerSuiteMax)
            .WithMessage($"A suite can have at most {ProbatioLimits.CasesPerSuiteMax} cases.")
            .Must(HaveDistinctIds).WithMessage("Two cases have the same id.");
        RuleForEach(x => x.Cases).SetValidator(new EvalCaseInputValidator(catalog));
    }

    private static bool HaveDistinctIds(IReadOnlyList<EvalCaseInput>? cases)
    {
        if (cases is null)
        {
            return true;
        }

        var ids = cases.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToList();
        return ids.Distinct().Count() == ids.Count;
    }
}
