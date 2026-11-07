using System.ComponentModel;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Tools.Attributes;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

public sealed class AgentToolFunctionTests
{
    [Fact]
    public void Schema_IsDerivedFromTheTypedMethod()
    {
        var function = new SearchTool().CreateFunction();

        function.Name.Should().Be("fake_search");
        var schema = function.JsonSchema;
        schema.GetProperty("properties").TryGetProperty("query", out _).Should().BeTrue();
        schema.GetProperty("properties").TryGetProperty("topK", out _).Should().BeTrue();
        schema.GetProperty("properties").TryGetProperty("ct", out _).Should().BeFalse("CancellationToken is bound by the framework, not the model");
        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().Equal("query");
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

        result?.ToString().Should().Be("vectors:3");
    }

    [Fact]
    public void ApprovalPolicy_WrapsFunction_AndClassificationSurvivesTheWrapper()
    {
        var function = new WriteTool().CreateFunction();

        function.Should().BeOfType<ApprovalRequiredAIFunction>();
        AgentToolFunction.TryGetClassification(function, out var resource, out var action).Should().BeTrue();
        resource.Should().Be(nameof(ToolResource.File));
        action.Should().Be(nameof(ToolAction.Write));
    }

    [Fact]
    public void FunctionsWithoutSentiumPolicy_HaveNoClassification()
    {
        var foreign = AIFunctionFactory.Create(() => "x", "load_skill");

        AgentToolFunction.TryGetClassification(foreign, out _, out _).Should().BeFalse();
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
