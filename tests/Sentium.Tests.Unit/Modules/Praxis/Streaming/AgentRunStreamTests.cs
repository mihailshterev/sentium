using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Streaming;

/// <summary>
/// One agent run turned into stream updates: activity before answer text within each update, the answer accumulated,
/// and the raw contents and first approval request kept for the caller.
/// </summary>
public sealed class AgentRunStreamTests
{
    private readonly RecordingSink _sink = new();

    [Fact]
    public async Task RunAsync_WritesActivityThenText_PerUpdate_AndAccumulatesTheAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = new AgentRunStream(_sink);

        await run.RunAsync(
            Updates(
                [new TextReasoningContent("Let me look.")],
                [new FunctionCallContent("c1", "find_assets"), new TextContent("Hel")],
                [new FunctionResultContent("c1", "Found 2")],
                [new TextContent("lo")]),
            ct);

        _sink.Writes.Select(w => w.Type).Should().Equal(
            AgentUpdateTypes.Thought,
            AgentUpdateTypes.ToolCall,
            AgentUpdateTypes.Message,
            AgentUpdateTypes.ToolResult,
            AgentUpdateTypes.Message);
        _sink.Writes.Where(w => w.Type == AgentUpdateTypes.Message).Select(w => w.Content).Should().Equal("Hel", "lo");
        run.Text.Should().Be("Hello");
        run.Contents.Should().HaveCount(5);
        run.Activity.Durations.Should().ContainKey("c1");
        run.ApprovalRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_KeepsTheFirstApprovalRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "execute_code_sandbox"));
        var second = new ToolApprovalRequestContent("r2", new FunctionCallContent("c2", "write_workspace_file"));
        var run = new AgentRunStream(_sink);

        await run.RunAsync(Updates([first], [second]), ct);

        run.ApprovalRequest.Should().BeSameAs(first);
    }

    [Fact]
    public async Task RunAsync_SkipsEmptyText()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = new AgentRunStream(_sink);

        await run.RunAsync(Updates([new TextContent(string.Empty)]), ct);

        _sink.Writes.Should().BeEmpty();
        run.Text.Should().BeEmpty();
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> Updates(params AIContent[][] updates)
    {
        foreach (var contents in updates)
        {
            await Task.Yield();
            yield return new AgentResponseUpdate(ChatRole.Assistant, [.. contents]);
        }
    }

    private sealed class RecordingSink : IAgentUpdateSink
    {
        public List<(string Type, string Content)> Writes { get; } = [];

        public Task WriteUpdateAsync(string type, string content, CancellationToken ct)
        {
            Writes.Add((type, content));
            return Task.CompletedTask;
        }
    }
}
