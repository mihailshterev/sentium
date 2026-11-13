using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Tools.Timeline;
using Sentium.Sentinel.Contracts;
using Sentium.Tempus.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

/// <summary>
/// The timeline tools reach Sentinel on the Timeline resource. The resource id is a task id or a date - never a
/// free-text title, which could trip the forbidden-verb and protected-prefix checks ("Drop off the car") - and the
/// write tools forward their arguments for content inspection.
/// </summary>
public sealed class SentinelTimelineToolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IPolicyDecisionPoint _pdp = Substitute.For<IPolicyDecisionPoint>();
    private PolicyEvaluationRequest? _captured;

    public SentinelTimelineToolTests()
    {
        _pdp.EvaluateAsync(Arg.Do<PolicyEvaluationRequest>(r => _captured = r), Arg.Any<CancellationToken>())
            .Returns(new PolicyEvaluationResponse
            {
                Allowed = true,
                Effect = "Allow",
                Reason = "ok",
                Risk = "Low",
                AuditId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    private static AIFunction CreateFunction(string toolName)
    {
        var timeline = Substitute.For<ITimelineAgentAccess>();
        var pdp = new PdpContextAccessor();
        var clock = TimeProvider.System;
        IAgentTool[] tools =
        [
            new GetTimelineOverviewTool(timeline, pdp, clock, NullLogger<GetTimelineOverviewTool>.Instance),
            new ListTimelineTasksTool(timeline, pdp, clock, NullLogger<ListTimelineTasksTool>.Instance),
            new GetTimelineTaskTool(timeline, pdp, clock, NullLogger<GetTimelineTaskTool>.Instance),
            new ProposeTimelineTaskTool(timeline, Substitute.For<IAgentRoster>(), pdp, clock, NullLogger<ProposeTimelineTaskTool>.Instance),
            new ProposeTaskRescheduleTool(timeline, pdp, clock, NullLogger<ProposeTaskRescheduleTool>.Instance),
            new AddTimelineNoteTool(timeline, pdp, clock, NullLogger<AddTimelineNoteTool>.Instance),
        ];

        return tools.Select(t => t.CreateFunction()).Single(f => f.Name == toolName);
    }

    public static TheoryData<string, Dictionary<string, object?>, string, string, bool> Calls => new()
    {
        { ToolNames.GetTimelineOverview, new() { ["year"] = "2026" }, "Read", "*", false },
        { ToolNames.ListTimelineTasks, new() { ["from"] = "2026-11-01" }, "Search", "*", false },
        { ToolNames.GetTimelineTask, new() { ["taskId"] = "0b9e7c1e-0000-0000-0000-000000000001" }, "Read", "0b9e7c1e-0000-0000-0000-000000000001", false },
        { ToolNames.ProposeTimelineTask, new() { ["title"] = "Drop off the car", ["date"] = "2026-11-12" }, "Write", "2026-11-12", true },
        { ToolNames.ProposeTimelineTask, new() { ["title"] = "Secret Santa shopping", ["month"] = "2026-12" }, "Write", "2026-12", true },
        { ToolNames.ProposeTaskReschedule, new() { ["taskId"] = "0b9e7c1e-0000-0000-0000-000000000002", ["date"] = "2026-11-20" }, "Write", "0b9e7c1e-0000-0000-0000-000000000002", true },
        { ToolNames.AddTimelineNote, new() { ["content"] = "Reviewed", ["month"] = "2026-11" }, "Write", "2026-11", true },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task TimelineTools_AreClassifiedOnTheTimelineResource(
        string toolName,
        Dictionary<string, object?> arguments,
        string expectedAction,
        string expectedResourceId,
        bool forwardsPayload)
    {
        var middleware = new SentinelFunctionMiddleware(
            new SentinelClient(_pdp, NullLogger<SentinelClient>.Instance),
            new PdpContextAccessor(),
            NullLogger<SentinelFunctionMiddleware>.Instance);

        var context = new FunctionInvocationContext
        {
            Function = CreateFunction(toolName),
            Arguments = new AIFunctionArguments(arguments),
        };

        var result = await middleware.InvokeAsync(
            new ChatClientAgent(Substitute.For<IChatClient>(), name: AgentRole.TimelinePlanner),
            context,
            (_, _) => ValueTask.FromResult<object?>("result"),
            Ct);

        result.Should().Be("result");
        _captured.Should().NotBeNull();
        _captured!.SkillName.Should().Be(toolName);
        _captured.ResourceType.Should().Be("Timeline");
        _captured.Action.Should().Be(expectedAction);
        _captured.ResourceId.Should().Be(expectedResourceId);

        if (forwardsPayload)
        {
            _captured.Metadata.Should().ContainKey("payload");
        }
        else
        {
            _captured.Metadata.Should().BeEmpty();
        }
    }

    [Fact]
    public void NoTimelineTool_IsApprovalGated_SoUnattendedRunsCanUseThem()
    {
        string[] names = [ToolNames.GetTimelineOverview, ToolNames.ListTimelineTasks, ToolNames.GetTimelineTask, ToolNames.ProposeTimelineTask, ToolNames.ProposeTaskReschedule, ToolNames.AddTimelineNote];

        foreach (var name in names)
        {
            CreateFunction(name).GetService<ApprovalRequiredAIFunction>().Should().BeNull(name);
        }
    }
}
