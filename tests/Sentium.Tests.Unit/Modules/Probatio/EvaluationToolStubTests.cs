using System.Text.Json;
using FluentAssertions;
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

        canned.Should().Be("milk, eggs");
        neutral.Should().Be(CannedToolStub.NeutralResult);
        stub.Calls.Select(c => (c.ToolName, c.Result)).Should().Equal(("Knowledge_Base_Search", "milk, eggs"), ("store_memory", CannedToolStub.NeutralResult));
        stub.Calls[0].Arguments.GetProperty("query").GetString().Should().Be("milk");
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

        function.Name.Should().Be("knowledge_base_search");
        function.Description.Should().Be("Searches the knowledge base.");
        function.JsonSchema.GetProperty("properties").GetProperty("query").GetProperty("type").GetString().Should().Be("string");
        result?.ToString().Should().Contain("stubbed");
        seen.Should().ContainSingle().Which.Item2["query"].Should().Be("milk");
    }
}
