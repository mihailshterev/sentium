using FluentValidation;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Agents;

internal static class AgentToolRules
{
    public const int MaxDescriptionLength = 1000;

    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> MustBeKnownTools<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule, IToolCatalog catalog)
    {
        return rule
            .Must(tools => tools is null || tools.Count <= catalog.GetTools().Count)
            .WithMessage("Too many tools.")
            .Must(tools => tools is null || tools.All(catalog.Contains))
            .WithMessage(_ => $"Unknown tool name. Known tools: {string.Join(", ", catalog.GetTools().Select(t => t.Name))}.");
    }
}
