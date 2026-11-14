using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Prompt and schema builders for the squad workflow. Every control decision is a small, single-purpose request with a
/// constrained (JSON-schema) answer; every hand-off between agents is a single compact user message rather than a
/// shared chat transcript - the two things that matter most for small local models.
/// </summary>
internal static class SquadPrompts
{
    /// <summary>
    /// Most steps a plan may contain - long pipelines compound small-model errors and latency.
    /// </summary>
    public const int MaxPlanSteps = 4;

    /// <summary>
    /// Character budget for earlier steps' results inside one step's input (roughly 1.5k tokens).
    /// </summary>
    private const int PriorResultsBudget = 6000;

    private const int MinPerResultBudget = 600;
    private const int ReviewedOutputBudget = 6000;

    /// <summary>
    /// Appended to a specialist's role when it runs as one step of a plan.
    /// </summary>
    public const string StepRules = """
        ### TEAM STEP
        You are one step of a team plan. Do only the task you are given, building on the earlier results provided.
        Tools that need human approval are not available in team runs - work with the tools you have.
        Output only your result - no preamble, no restating the request, no status lines.
        """;

    public const string SynthesisInstructions = """
        You combine the results of a team of specialists into one final answer to the user's request.
        Keep every correct detail, remove repetition, resolve contradictions in favour of verified results, and do not
        add new claims. Output only the final answer.
        """;

    public const string AssessmentInstructions = """
        You assess a final answer before it is shown to the user. Rate the risk of acting on it - Low, Medium, High,
        or Critical - considering accuracy and completeness, and give a one-sentence recommendation.
        """;

    public static string PlannerInput(string request, IReadOnlyList<SquadMember> roster)
    {
        var sb = new StringBuilder();
        sb.Append("## Roster\n");
        foreach (var member in roster)
        {
            sb.Append("- ").Append(member.Name).Append(": ").Append(OneLine(member.Description, 200)).Append('\n');
        }

        sb.Append("\n## Request\n").Append(request.Trim());
        return sb.ToString();
    }

    /// <summary>
    /// JSON schema for <see cref="SquadPlan"/> whose agent field is an <c>enum</c> of the roster names, so constrained
    /// decoding makes it impossible for the planner to invent an agent.
    /// </summary>
    public static ChatResponseFormat PlanFormat(IReadOnlyList<SquadMember> roster)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["steps"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["minItems"] = 0,
                    ["maxItems"] = MaxPlanSteps,
                    ["items"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["agent"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = roster.Select(m => m.Name).ToArray() },
                            ["task"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["expectedOutput"] = new Dictionary<string, object> { ["type"] = "string" },
                        },
                        ["required"] = new[] { "agent", "task", "expectedOutput" },
                        ["additionalProperties"] = false,
                    },
                },
            },
            ["required"] = new[] { "steps" },
            ["additionalProperties"] = false,
        };

        return ChatResponseFormat.ForJsonSchema(JsonSerializer.SerializeToElement(schema), nameof(SquadPlan), "An ordered plan of specialist steps.");
    }

    public static string StepInput(StepRequest request)
    {
        var step = request.Step;
        var plan = request.State.Plan;
        var sb = new StringBuilder();

        sb.Append("## Request\n").Append(request.State.Request.Trim()).Append("\n\n");
        sb.Append("## Your task (step ").Append(request.StepIndex + 1).Append(" of ").Append(plan.Steps.Count).Append(")\n").Append(step.Task.Trim()).Append("\n\n");
        sb.Append("## Expected output\n").Append(step.ExpectedOutput.Trim());

        var prior = request.State.Completed;
        if (prior.Count > 0)
        {
            var perResult = Math.Max(MinPerResultBudget, PriorResultsBudget / prior.Count);
            sb.Append("\n\n## Results from earlier steps");
            foreach (var result in prior)
            {
                sb.Append("\n### ").Append(result.Step.Agent).Append(" - ").Append(OneLine(result.Step.Task, 160)).Append('\n');
                sb.Append(Truncate(result.Output, perResult));
            }
        }

        if (request.Feedback.Count > 0)
        {
            sb.Append("\n\n## Reviewer feedback on your previous attempt - fix these\n");
            foreach (var issue in request.Feedback)
            {
                sb.Append("- ").Append(OneLine(issue, 300)).Append('\n');
            }

            if (!string.IsNullOrWhiteSpace(request.PreviousOutput))
            {
                sb.Append("\nYour previous attempt (for reference - produce a corrected version):\n").Append(Truncate(request.PreviousOutput, PriorResultsBudget / 2));
            }
        }

        return sb.ToString();
    }

    public static string CheckInput(SquadStep step, string output)
    {
        return $"""
            ## Task
            {step.Task.Trim()}

            ## Expected output
            {step.ExpectedOutput.Trim()}

            ## Output to review
            {Truncate(output, ReviewedOutputBudget)}
            """;
    }

    public static string SynthesisInput(string request, IReadOnlyList<StepResult> results)
    {
        var sb = new StringBuilder("## Request\n").Append(request.Trim()).Append("\n\n## Team results");
        var perResult = Math.Max(MinPerResultBudget, PriorResultsBudget / Math.Max(1, results.Count));

        foreach (var result in results)
        {
            sb.Append("\n### ").Append(result.Step.Agent).Append(result.Verified ? string.Empty : " (unverified)").Append('\n');
            sb.Append(Truncate(result.Output, perResult));
        }

        return sb.ToString();
    }

    public static string AssessmentInput(string request, string answer)
    {
        return $"""
            ## Request
            {request.Trim()}

            ## Final answer
            {Truncate(answer, ReviewedOutputBudget)}
            """;
    }

    public static string DescribePlan(SquadPlan plan) =>
        string.Join(" → ", plan.Steps.Select((s, i) => $"{i + 1}. {s.Agent}: {OneLine(s.Task, 120)}"));

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        return trimmed.Length > max ? string.Concat(trimmed.AsSpan(0, max), "…") : trimmed;
    }

    private static string OneLine(string? text, int max) => Truncate(text?.ReplaceLineEndings(" "), max);
}
