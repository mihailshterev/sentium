using FluentValidation;
using Sentium.Praxis.Contracts;

namespace Sentium.Probatio.Features.Suites;

internal sealed class UpdateEvalSuiteRequestValidator : AbstractValidator<UpdateEvalSuiteRequest>
{
    public UpdateEvalSuiteRequestValidator(IToolCatalog catalog)
    {
        Include(new EvalSuiteContentValidator(catalog));
        RuleFor(x => x.BaseVersion).GreaterThanOrEqualTo(1);
    }
}
