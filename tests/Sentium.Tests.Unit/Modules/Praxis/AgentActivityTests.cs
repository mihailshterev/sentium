using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Activity;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Tests.Unit.Modules.Praxis;

/// <summary>
/// The activity timeline: live events keep the order thinking → call → result → thinking, and the persisted steps
/// rebuilt from the raw turn match it.
/// </summary>
public sealed class AgentActivityTests
{
    [Fact]
    public void Translator_EmitsOrderedEvents_WithArgumentsStatusAndDuration()
    {
        var time = new ManualClock();
        var translator = new AgentActivityTranslator(time);

        var events = new List<AgentActivityEvent>();
        events.AddRange(translator.Translate([new TextReasoningContent("Let me look. ")]));
        events.AddRange(translator.Translate([new FunctionCallContent("c1", "find_assets", new Dictionary<string, object?> { ["query"] = "drill" })]));
        time.Advance(TimeSpan.FromMilliseconds(420));
        events.AddRange(translator.Translate([new FunctionResultContent("c1", "Found 2 assets")]));
        events.AddRange(translator.Translate([new TextReasoningContent("Two hits.")]));

        events.Select(e => e.Type).Should().Equal(AgentUpdateTypes.Thought, AgentUpdateTypes.ToolCall, AgentUpdateTypes.ToolResult, AgentUpdateTypes.Thought);

        using var call = JsonDocument.Parse(events[1].Payload);
        call.RootElement.GetProperty("callId").GetString().Should().Be("c1");
        call.RootElement.GetProperty("name").GetString().Should().Be("find_assets");
        call.RootElement.GetProperty("arguments").GetString().Should().Contain("drill");

        using var result = JsonDocument.Parse(events[2].Payload);
        result.RootElement.GetProperty("status").GetString().Should().Be("ok");
        result.RootElement.GetProperty("result").GetString().Should().Be("Found 2 assets");
        result.RootElement.GetProperty("durationMs").GetInt64().Should().Be(420);
        translator.Durations["c1"].Should().Be(420);
    }

    [Fact]
    public void Translator_MarksPolicyDenialsAndExceptions()
    {
        var translator = new AgentActivityTranslator();
        translator.Translate([new FunctionCallContent("a", "write_workspace_file"), new FunctionCallContent("b", "read_file")]).ToList();

        var denied = translator.Translate([new FunctionResultContent("a", "[Access Denied by Security Policy] egress")]).Single();
        var failed = translator.Translate([new FunctionResultContent("b", result: null) { Exception = new InvalidOperationException("disk gone") }]).Single();

        JsonDocument.Parse(denied.Payload).RootElement.GetProperty("status").GetString().Should().Be("denied");
        var error = JsonDocument.Parse(failed.Payload).RootElement;
        error.GetProperty("status").GetString().Should().Be("error");
        error.GetProperty("result").GetString().Should().Be("disk gone");
    }

    [Fact]
    public void Translator_EmitsEachCallOnce()
    {
        var translator = new AgentActivityTranslator();
        var call = new FunctionCallContent("c1", "find_assets");

        translator.Translate([call]).Concat(translator.Translate([call])).Should().ContainSingle();
    }

    [Fact]
    public void Steps_KeepOrder_NarrationBeforeToolsBecomesTextSteps_AndTheTailIsTheAnswer()
    {
        ChatMessage[] turn =
        [
            new(ChatRole.Assistant, [new TextReasoningContent("Plan: "), new TextReasoningContent("search first."), new TextContent("Searching now."), new FunctionCallContent("c1", "knowledge_base_search", new Dictionary<string, object?> { ["query"] = "hnsw" })]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", "3 snippets")]),
            new(ChatRole.Assistant, [new TextReasoningContent("Enough context."), new TextContent("Use HNSW.")]),
        ];

        var (answer, steps) = AgentTurnSteps.Build(turn, new Dictionary<string, long> { ["c1"] = 87 });

        answer.Should().Be("Use HNSW.");
        steps.Select(s => s.Kind).Should().Equal("thought", "text", "tool", "thought");
        steps[0].Text.Should().Be("Plan: search first.");
        steps[1].Text.Should().Be("Searching now.");
        steps[2].Should().BeEquivalentTo(new { Name = "knowledge_base_search", Status = "ok", Result = "3 snippets", DurationMs = 87L });
        steps[2].Arguments.Should().Contain("hnsw");
    }

    [Fact]
    public void Steps_WithoutTrailingText_PromoteTheLastNarrationToTheAnswer()
    {
        ChatMessage[] turn =
        [
            new(ChatRole.Assistant, [new TextContent("Saving your note now."), new FunctionCallContent("c1", "add_timeline_note")]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", "saved")]),
        ];

        var (answer, steps) = AgentTurnSteps.Build(turn);

        answer.Should().BeEmpty("the narration precedes the tool, so it stays on the timeline");
        steps.Select(s => s.Kind).Should().Equal("text", "tool");

        var (tailAnswer, tailSteps) = AgentTurnSteps.Build([new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c2", "find_assets"), new TextContent("Nothing found.")])]);
        tailAnswer.Should().Be("Nothing found.");
        tailSteps.Should().ContainSingle(s => s.Kind == "tool");
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
