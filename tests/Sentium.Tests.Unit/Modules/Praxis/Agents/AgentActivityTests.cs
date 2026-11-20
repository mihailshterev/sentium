using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents.Activity;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

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

        Assert.Equal(new string[] { AgentUpdateTypes.Thought, AgentUpdateTypes.ToolCall, AgentUpdateTypes.ToolResult, AgentUpdateTypes.Thought }, events.Select(e => e.Type));

        using var call = JsonDocument.Parse(events[1].Payload);
        Assert.Equal("c1", call.RootElement.GetProperty("callId").GetString());
        Assert.Equal("find_assets", call.RootElement.GetProperty("name").GetString());
        Assert.Contains("drill", call.RootElement.GetProperty("arguments").GetString(), StringComparison.Ordinal);

        using var result = JsonDocument.Parse(events[2].Payload);
        Assert.Equal("ok", result.RootElement.GetProperty("status").GetString());
        Assert.Equal("Found 2 assets", result.RootElement.GetProperty("result").GetString());
        Assert.Equal(420, result.RootElement.GetProperty("durationMs").GetInt64());
        Assert.Equal(420, translator.Durations["c1"]);
    }

    [Fact]
    public void Translator_MarksPolicyDenialsAndExceptions()
    {
        var translator = new AgentActivityTranslator();
        translator.Translate([new FunctionCallContent("a", "write_workspace_file"), new FunctionCallContent("b", "read_file")]).ToList();

        var denied = translator.Translate([new FunctionResultContent("a", "[Access Denied by Security Policy] egress")]).Single();
        var failed = translator.Translate([new FunctionResultContent("b", result: null) { Exception = new InvalidOperationException("disk gone") }]).Single();

        Assert.Equal("denied", JsonDocument.Parse(denied.Payload).RootElement.GetProperty("status").GetString());
        var error = JsonDocument.Parse(failed.Payload).RootElement;
        Assert.Equal("error", error.GetProperty("status").GetString());
        Assert.Equal("disk gone", error.GetProperty("result").GetString());
    }

    [Fact]
    public void Translator_EmitsEachCallOnce()
    {
        var translator = new AgentActivityTranslator();
        var call = new FunctionCallContent("c1", "find_assets");

        Assert.Single(translator.Translate([call]).Concat(translator.Translate([call])));
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

        Assert.Equal("Use HNSW.", answer);
        Assert.Equal(new string[] { "thought", "text", "tool", "thought" }, steps.Select(s => s.Kind));
        Assert.Equal("Plan: search first.", steps[0].Text);
        Assert.Equal("Searching now.", steps[1].Text);
        Assert.Equivalent(new { Name = "knowledge_base_search", Status = "ok", Result = "3 snippets", DurationMs = 87L }, steps[2]);
        Assert.Contains("hnsw", steps[2].Arguments, StringComparison.Ordinal);
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

        // the narration precedes the tool, so it stays on the timeline
        Assert.Empty(answer);
        Assert.Equal(new string[] { "text", "tool" }, steps.Select(s => s.Kind));

        var (tailAnswer, tailSteps) = AgentTurnSteps.Build([new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c2", "find_assets"), new TextContent("Nothing found.")])]);
        Assert.Equal("Nothing found.", tailAnswer);
        Assert.Single(tailSteps, s => s.Kind == "tool");
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
