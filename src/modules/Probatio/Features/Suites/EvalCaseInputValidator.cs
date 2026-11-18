using FluentValidation;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Common;

namespace Sentium.Probatio.Features.Suites;

internal sealed class EvalCaseInputValidator : AbstractValidator<EvalCaseInput>
{
    public EvalCaseInputValidator(IToolCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        RuleFor(x => x.Id).Must(id => id != Guid.Empty).When(x => x.Id.HasValue).WithMessage("Case id is invalid.");
        RuleFor(x => x.Name).MaximumLength(ProbatioLimits.CaseNameMax);
        RuleFor(x => x.Prompt)
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("A case needs a prompt.")
            .MaximumLength(ProbatioLimits.PromptMax);
        RuleFor(x => x.ExpectedAnswer).MaximumLength(ProbatioLimits.ExpectedAnswerMax);

        AddPriorTurnRules();
        AddToolRules(catalog);
        AddCheckRules();
    }

    private void AddPriorTurnRules()
    {
        RuleFor(x => x.PriorTurns)
            .Must(t => t is null || t.Count <= ProbatioLimits.PriorTurnsMax)
            .WithMessage($"A case can have at most {ProbatioLimits.PriorTurnsMax} prior turns.");
        RuleForEach(x => x.PriorTurns).ChildRules(turn =>
        {
            turn.RuleFor(t => t.Role).IsInEnum();
            turn.RuleFor(t => t.Text)
                .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("A prior turn cannot be empty.")
                .MaximumLength(ProbatioLimits.TurnTextMax);
        });
    }

    private void AddToolRules(IToolCatalog catalog)
    {
        RuleFor(x => x.ExpectedToolCalls)
            .Must(c => c is null || c.Count <= ProbatioLimits.ToolCallsPerCaseMax)
            .WithMessage($"A case can expect at most {ProbatioLimits.ToolCallsPerCaseMax} tool calls.");
        RuleForEach(x => x.ExpectedToolCalls).ChildRules(call =>
        {
            KnownTool(call.RuleFor(c => c.ToolName), catalog);
            call.RuleFor(c => c.Arguments)
                .Must(a => a is null || a.Count <= ProbatioLimits.ArgumentsPerToolCallMax)
                .WithMessage($"An expected tool call can check at most {ProbatioLimits.ArgumentsPerToolCallMax} arguments.")
                .Must(a => a is null || a.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= ProbatioLimits.ArgumentNameMax))
                .WithMessage("Argument names must be non-empty and short.");
        });

        RuleFor(x => x.CannedToolResults)
            .Must(r => r is null || r.Count <= ProbatioLimits.ToolCallsPerCaseMax)
            .WithMessage($"A case can have at most {ProbatioLimits.ToolCallsPerCaseMax} canned tool results.")
            .Must(r => r is null || r.Select(c => c.ToolName?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Count)
            .WithMessage("A tool can have only one canned result per case.");
        RuleForEach(x => x.CannedToolResults).ChildRules(result =>
        {
            KnownTool(result.RuleFor(r => r.ToolName), catalog);
            result.RuleFor(r => r.Result).NotNull().MaximumLength(ProbatioLimits.CannedResultMax);
        });
    }

    private void AddCheckRules()
    {
        RuleFor(x => x.Checks)
            .Must(c => c is { Count: > 0 }).WithMessage("A case needs at least one Check.")
            .Must(c => c is null || c.Count <= ProbatioLimits.ChecksPerCaseMax)
            .WithMessage($"A case can have at most {ProbatioLimits.ChecksPerCaseMax} Checks.")
            .Must(c => c is null || c.Select(check => check.Type).Distinct().Count() == c.Count)
            .WithMessage("A case cannot have the same Check twice.");

        RuleForEach(x => x.Checks).ChildRules(check =>
        {
            check.RuleFor(c => c.Type).IsInEnum().WithMessage("Unknown Check type.");
            check.RuleFor(c => c.Threshold)
                .Null()
                .When(c => !EvalCheckRules.IsQuality(c.Type))
                .WithMessage("Only quality Checks have a threshold.");
            check.RuleFor(c => c.Threshold)
                .InclusiveBetween(ProbatioLimits.QualityScoreMin, ProbatioLimits.QualityScoreMax)
                .When(c => EvalCheckRules.IsQuality(c.Type) && c.Threshold.HasValue);
        });

        RuleForEach(x => x.Checks)
            .Must((input, check) => !EvalCheckRules.NeedsExpectedAnswer(check.Type) || !string.IsNullOrWhiteSpace(input.ExpectedAnswer))
            .WithMessage((_, check) => $"The {check.Type} Check needs an expected answer.")
            .Must((input, check) => !EvalCheckRules.NeedsExpectedToolCalls(check.Type) || input.ExpectedToolCalls is { Count: > 0 })
            .WithMessage((_, check) => $"The {check.Type} Check needs at least one expected tool call.")
            .Must((input, check) => !EvalCheckRules.NeedsCannedToolResults(check.Type) || input.CannedToolResults is { Count: > 0 })
            .WithMessage((_, check) => $"The {check.Type} Check needs at least one canned tool result to judge against.");
    }

    private static void KnownTool<T>(IRuleBuilder<T, string> rule, IToolCatalog catalog)
    {
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("A tool name is required.")
            .MaximumLength(ProbatioLimits.ToolNameMax)
            .Must(name => string.IsNullOrWhiteSpace(name) || catalog.Contains(name.Trim()))
            .WithMessage((_, name) => $"Unknown tool '{name}'.");
    }
}
