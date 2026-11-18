using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Core.Results;

namespace Sentium.Praxis.Contracts;

/// <summary>
/// Builds agents for Evaluations. The built agent is given only stub functions made from the snapshot's tool
/// declarations, so no real tool - built-in, MCP or any future source - can execute (ADR 0003).
/// </summary>
public interface IEvaluationAgentBuilder
{
    /// <summary>
    /// Freezes <paramref name="agentName"/> as <paramref name="userId"/> would get it, on <paramref name="model"/>
    /// (<see langword="null"/>: the user's default model). Invalid when the user has no such agent or the model is
    /// not in the model catalog.
    /// </summary>
    Task<Result<EvaluationAgentSnapshot>> SnapshotAsync(string agentName, string? model, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// An agent behaving like <paramref name="snapshot"/> whose every tool call is answered by <paramref name="stub"/>.
    /// Built-in skills stay available; their scripts are side-effect-free application code.
    /// </summary>
    Task<AIAgent> CreateAsync(EvaluationAgentSnapshot snapshot, EvaluationToolStub stub, CancellationToken ct = default);

    /// <summary>
    /// The model <paramref name="model"/> stands for (<see langword="null"/>: the user's default model). Invalid
    /// when it is not in the model catalog.
    /// </summary>
    Task<Result<string>> ResolveModelAsync(string? model, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The ids of the models the catalog serves now; <see langword="null"/> when the catalog is unavailable. Used to
    /// show Evaluations of a model that has since been deleted.
    /// </summary>
    Task<IReadOnlySet<string>?> ListModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// A chat client for the Judge: every request it sends runs on <paramref name="model"/>. It shares the
    /// singleton agent base client, so never dispose it.
    /// </summary>
    IChatClient CreateJudgeClient(string model);
}
