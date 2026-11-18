using Sentium.Praxis.Contracts;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// What an Evaluation, or every Evaluation of a Benchmark, is started from: the suite version, the Judge (resolved
/// once, so a Benchmark grades every model alike) and a frozen copy of the agent per model.
/// </summary>
internal sealed record EvaluationLaunch(EvalSuite Suite, EvalSuiteVersion Version, string? JudgeModel, IReadOnlyList<EvaluationAgentSnapshot> Agents);
