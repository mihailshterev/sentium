using FluentValidation;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Agents;

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentRequestValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(AgentToolRules.MaxDescriptionLength);

        RuleFor(x => x.Model)
            .MaximumLength(100);

        RuleFor(x => x.Tools).MustBeKnownTools(catalog);
    }
}

public sealed class UpdateAgentRequestValidator : AbstractValidator<UpdateAgentRequest>
{
    public UpdateAgentRequestValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(AgentToolRules.MaxDescriptionLength);

        RuleFor(x => x.Model)
            .MaximumLength(100);

        RuleFor(x => x.Tools).MustBeKnownTools(catalog);
    }
}

public sealed class UpdateAgentToolsRequestValidator : AbstractValidator<UpdateAgentToolsRequest>
{
    public UpdateAgentToolsRequestValidator(IToolCatalog catalog)
    {
        RuleFor(x => x.Tools).MustBeKnownTools(catalog);
    }
}

internal static class AgentToolRules
{
    public const int MaxDescriptionLength = 1000;

    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> MustBeKnownTools<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule, IToolCatalog catalog) =>
        rule
            .Must(tools => tools is null || tools.Count <= catalog.GetTools().Count)
            .WithMessage("Too many tools.")
            .Must(tools => tools is null || tools.All(catalog.Contains))
            .WithMessage(_ => $"Unknown tool name. Known tools: {string.Join(", ", catalog.GetTools().Select(t => t.Name))}.");
}
