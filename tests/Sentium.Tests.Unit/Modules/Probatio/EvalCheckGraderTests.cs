using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Evaluations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Probatio;

public sealed class EvalCheckGraderTests
{
    private const string Tool = "knowledge_base_search";

    private static EvalCase Case(string? expectedAnswer = null, IReadOnlyDictionary<string, JsonElement>? arguments = null, params EvalCheckType[] checks) => new(
        Guid.NewGuid(),
        null,
        "What do we need?",
        [],
        expectedAnswer,
        [new ExpectedToolCall(Tool, arguments)],
        [],
        [.. checks.Select(c => new EvalCheck(c))]);

    /// <summary>
    /// A conversation as the OpenAI-compatible client produces it: call arguments parsed into JSON elements.
    /// </summary>
    private static List<ChatMessage> Conversation(string answer, string? toolArgumentsJson)
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, "What do we need?") };
        if (toolArgumentsJson is not null)
        {
            var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolArgumentsJson)!
                .ToDictionary(a => a.Key, a => (object?)a.Value);
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", Tool, arguments)]));
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "milk, eggs")]));
        }

        messages.Add(new ChatMessage(ChatRole.Assistant, answer));
        return messages;
    }

    private static CheckOutcomeStatus Grade(EvalCase evalCase, string answer, string? toolArgumentsJson)
    {
        var item = EvalCheckGrader.ToItem(evalCase, answer, Conversation(answer, toolArgumentsJson));
        return EvalCheckGrader.Grade(evalCase.Checks.Single(), evalCase, item).Status;
    }

    private static Dictionary<string, JsonElement> Args(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void ContainsExpectedAnswer_IgnoresCase()
    {
        var evalCase = Case("Milk", checks: EvalCheckType.ContainsExpectedAnswer);

        Assert.Equal(CheckOutcomeStatus.Passed, Grade(evalCase, "You still need milk and eggs.", null));
        Assert.Equal(CheckOutcomeStatus.Failed, Grade(evalCase, "Nothing.", null));
    }

    [Fact]
    public void ToolsCalled_NeedsEveryExpectedTool()
    {
        var evalCase = Case(checks: EvalCheckType.ToolsCalled);

        Assert.Equal(CheckOutcomeStatus.Passed, Grade(evalCase, "Milk.", """{"query":"milk"}"""));
        Assert.Equal(CheckOutcomeStatus.Failed, Grade(evalCase, "Milk.", null));
    }

    [Theory]
    [InlineData("""{"query":"milk"}""", """{"query":"milk","limit":5}""", true)]
    [InlineData("""{"query":"milk"}""", """{"query":"bread"}""", false)]
    [InlineData("""{"limit":5}""", """{"query":"milk","limit":5}""", true)]
    [InlineData("""{"limit":5}""", """{"query":"milk","limit":6}""", false)]
    [InlineData("""{"exact":true}""", """{"exact":true}""", true)]
    public void ToolArgumentsMatch_IsASubsetMatchOnValues(string expected, string actual, bool passes)
    {
        var evalCase = Case(arguments: Args(expected), checks: EvalCheckType.ToolArgumentsMatch);

        Assert.Equal(passes ? CheckOutcomeStatus.Passed : CheckOutcomeStatus.Failed, Grade(evalCase, "Milk.", actual));
    }
}
