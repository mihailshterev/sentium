using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// Builds the <see cref="WorkspaceContextProvider"/> attached to each agent, sizing how much attached-file text it may
/// inject from the model's context window.
/// </summary>
internal sealed class WorkspaceContextProviderFactory(IWorkspaceContextBuilder builder, IPdpContextAccessor runContext)
{
    /// <summary>
    /// A rough average for English text and code; the budget only needs to be in the right range.
    /// </summary>
    private const int CharsPerToken = 4;

    /// <summary>
    /// The share of the context window attached files may take, leaving room for instructions, history, tool results
    /// and the answer.
    /// </summary>
    private const double AttachmentShare = 0.35;

    private const int MinBudgetChars = 4_000;

    private const int MaxBudgetChars = 160_000;

    public WorkspaceContextProvider Create(int contextWindowTokens)
    {
        return new WorkspaceContextProvider(builder, runContext, BudgetFor(contextWindowTokens));
    }

    internal static int BudgetFor(int contextWindowTokens)
    {
        var budget = (long)(contextWindowTokens * (double)CharsPerToken * AttachmentShare);
        return (int)Math.Clamp(budget, MinBudgetChars, MaxBudgetChars);
    }
}
