using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class AgentToolFunctionTests
{
    [Fact]
    public void Schema_IsDerivedFromTheTypedMethod()
    {
        var function = new SearchTool().CreateFunction();

        Assert.Equal("fake_search", function.Name);
        var schema = function.JsonSchema;
        Assert.True(schema.GetProperty("properties").TryGetProperty("query", out _));
        Assert.True(schema.GetProperty("properties").TryGetProperty("topK", out _));
        Assert.False(schema.GetProperty("properties").TryGetProperty("ct", out _), "CancellationToken is bound by the framework, not the model");
        Assert.Equal(new string[] { "query" }, schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Arguments_AreBoundLeniently_ForSmallModels()
    {
        var ct = TestContext.Current.CancellationToken;
        var function = new SearchTool().CreateFunction();

        // Small models frequently quote numbers; binding must still succeed.
        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            ["query"] = JsonDocument.Parse("\"vectors\"").RootElement,
            ["topK"] = JsonDocument.Parse("\"3\"").RootElement,
        }, ct);

        Assert.Equal("vectors:3", result?.ToString());
    }

    [Fact]
    public void ApprovalPolicy_WrapsFunction_AndClassificationSurvivesTheWrapper()
    {
        var function = new WriteTool().CreateFunction();

        Assert.IsType<ApprovalRequiredAIFunction>(function);
        Assert.True(AgentToolFunction.TryGetClassification(function, out var resource, out var action));
        Assert.Equal(nameof(ToolResource.File), resource);
        Assert.Equal(nameof(ToolAction.Write), action);
    }

    [Fact]
    public void FunctionsWithoutSentiumPolicy_HaveNoClassification()
    {
        var foreign = AIFunctionFactory.Create(() => "x", "load_skill");

        Assert.False(AgentToolFunction.TryGetClassification(foreign, out _, out _));
    }

    [AgentToolPolicy(Resource = ToolResource.VectorDb, Action = ToolAction.Search)]
    private sealed class SearchTool : IAgentTool
    {
        public AIFunction CreateFunction() => AgentToolFunction.Create(this, SearchAsync, "fake_search", "Fake search.");

        private static Task<string> SearchAsync([Description("Query.")] string query, [Description("Limit.")] int? topK = null, CancellationToken ct = default)
            => Task.FromResult($"{query}:{topK}");
    }

    [AgentToolPolicy(RequiresApproval = true, Resource = ToolResource.File, Action = ToolAction.Write)]
    private sealed class WriteTool : IAgentTool
    {
        public AIFunction CreateFunction() => AgentToolFunction.Create(this, Write, "fake_write", "Fake write.");

        private static string Write([Description("Content.")] string content) => content;
    }
}
