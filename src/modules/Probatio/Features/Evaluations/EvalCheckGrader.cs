using System.Text.Json;
using Microsoft.Agents.AI;
using Sentium.Probatio.Entities;
using AfEvalCheck = Microsoft.Agents.AI.EvalCheck;
using AfExpectedToolCall = Microsoft.Agents.AI.ExpectedToolCall;
using EvalCheck = Sentium.Probatio.Entities.EvalCheck;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// Grades an answer against a case's deterministic Checks with Agent Framework's <see cref="EvalChecks"/>. Quality
/// Checks are scored by <see cref="QualityCheckGrader"/>.
/// </summary>
internal static class EvalCheckGrader
{
    /// <summary>
    /// The evaluation item for an answer: the conversation it ended plus the case's expected answer and tool calls.
    /// </summary>
    public static EvalItem ToItem(EvalCase evalCase, string response, IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> conversation)
    {
        return new EvalItem(evalCase.Prompt, response, conversation)
        {
            ExpectedOutput = evalCase.ExpectedAnswer,
            ExpectedToolCalls = [.. evalCase.ExpectedToolCalls.Select(ToExpectedCall)],
        };
    }

    public static CheckOutcome Grade(EvalCheck check, EvalCase evalCase, EvalItem item)
    {
        AfEvalCheck grader = check.Type switch
        {
            EvalCheckType.ContainsExpectedAnswer => EvalChecks.ContainsExpected(),
            EvalCheckType.ToolsCalled => EvalChecks.ToolCalledCheck([.. evalCase.ExpectedToolCalls.Select(c => c.ToolName).Distinct(StringComparer.Ordinal)]),
            EvalCheckType.ToolArgumentsMatch => EvalChecks.ToolCallArgsMatch(),
            _ => throw new InvalidOperationException($"{check.Type} is not a deterministic Check."),
        };

        var result = grader(item);
        return new CheckOutcome(check.Type, result.Passed ? CheckOutcomeStatus.Passed : CheckOutcomeStatus.Failed, result.Reason);
    }

    private static AfExpectedToolCall ToExpectedCall(Entities.ExpectedToolCall call)
    {
        var arguments = call.Arguments?.ToDictionary(a => a.Key, a => ToValue(a.Value), StringComparer.Ordinal);
        return new AfExpectedToolCall(call.ToolName, arguments);
    }

    /// <summary>
    /// Expected argument values as plain CLR values (string, number, bool) so they compare equal to the values the
    /// model's call is parsed into; objects and arrays stay JSON.
    /// </summary>
    private static object ToValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.TryGetInt64(out var whole) ? whole : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value,
    };
}
