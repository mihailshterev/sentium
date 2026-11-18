namespace Sentium.Praxis.Contracts;

/// <summary>
/// An agent frozen for an Evaluation: what it would get in a chat on <paramref name="Model"/> - the composed system
/// instructions (harness, role, tool list), its tools as declarations, its temperature and the user's skills - minus
/// Learnings, stored history, workspace files, approvals and Sentinel. Built-in skills are application code and are
/// not part of the snapshot.
/// </summary>
public sealed record EvaluationAgentSnapshot(
    string AgentName,
    string Model,
    string Instructions,
    float? Temperature,
    IReadOnlyList<EvaluationToolDeclaration> Tools,
    IReadOnlyList<EvaluationSkill> UserSkills);
