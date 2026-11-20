using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Evaluations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Probatio;

public sealed class EvaluationToolStubTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CannedStub_AnswersWithTheCannedResult_OrANeutralOne_AndRecordsEveryCall()
    {
        var stub = new CannedToolStub([new CannedToolResult("knowledge_base_search", "milk, eggs")]);

        var canned = await stub.AnswerAsync("Knowledge_Base_Search", new Dictionary<string, object?> { ["query"] = "milk" }, Ct);
        var neutral = await stub.AnswerAsync("store_memory", new Dictionary<string, object?>(), Ct);

        Assert.Equal("milk, eggs", canned);
        Assert.Equal(CannedToolStub.NeutralResult, neutral);
        Assert.Equal(new (string ToolName, string Result)[] { ("Knowledge_Base_Search", "milk, eggs"), ("store_memory", CannedToolStub.NeutralResult) }, stub.Calls.Select(c => (c.ToolName, c.Result)));
        Assert.Equal("milk", stub.Calls[0].Arguments.GetProperty("query").GetString());
    }

    [Fact]
    public async Task StubFunction_KeepsTheDeclaration_AndOnlyCallsTheStub()
    {
        using var schema = JsonDocument.Parse("""{"type":"object","properties":{"query":{"type":"string"}}}""");
        var declaration = new EvaluationToolDeclaration("knowledge_base_search", "Searches the knowledge base.", schema.RootElement.Clone());
        var seen = new List<(string, IReadOnlyDictionary<string, object?>)>();
        EvaluationToolStub stub = (name, arguments, _) =>
        {
            seen.Add((name, arguments));
            return ValueTask.FromResult("stubbed");
        };

        var function = new EvaluationStubFunction(declaration, stub);
        var result = await function.InvokeAsync(new AIFunctionArguments { ["query"] = "milk" }, Ct);

        Assert.Equal("knowledge_base_search", function.Name);
        Assert.Equal("Searches the knowledge base.", function.Description);
        Assert.Equal("string", function.JsonSchema.GetProperty("properties").GetProperty("query").GetProperty("type").GetString());
        Assert.Contains("stubbed", result?.ToString(), StringComparison.Ordinal);
        Assert.Equal("milk", Assert.Single(seen).Item2["query"]);
    }
}
