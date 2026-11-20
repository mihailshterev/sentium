using System.Collections.Specialized;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Sentium.Core.Constants;
using Sentium.Core.Results;
using Sentium.Praxis.Contracts;
using Sentium.Registry.Contracts;
using Sentium.Scheduling.Features.Jobs;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Scheduling;

/// <summary>
/// Agent and workflow Schedules against a real (unstarted, in-memory) Quartz scheduler.
/// </summary>
public sealed class ScheduleServiceTests : IAsyncLifetime
{
    private const string Daily0730 = "0 30 7 ? * *";
    private const string Weekly = "0 0 18 ? * SUN";
    private const string Sofia = "Europe/Sofia";

    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly ISchedulerFactory _factory = new StdSchedulerFactory(new NameValueCollection
    {
        ["quartz.scheduler.instanceName"] = $"unit-{Guid.NewGuid():N}",
        ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
    });

    private readonly IUnattendedRunLauncher _launcher = Substitute.For<IUnattendedRunLauncher>();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private IScheduler _scheduler = null!;
    private ScheduleService _service = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _scheduler = await _factory.GetScheduler(Ct);

        _launcher.ResolveTargetAsync(
                UnattendedRunKind.Agent,
                Arg.Is<string?>(n => string.Equals(n, "TimelinePlanner", StringComparison.OrdinalIgnoreCase)),
                Arg.Any<Guid?>(),
                Alice,
                Arg.Any<CancellationToken>())
            .Returns(new UnattendedRunTarget("TimelinePlanner", [Gated("execute_code_sandbox"), Gated("schedule_recurring_task")]));

        _settings.GetAsync(SettingsKeys.UserCalendar, Alice, Arg.Any<CancellationToken>())
            .Returns(new SettingsEnvelope(SettingsKeys.UserCalendar, JsonSerializer.SerializeToElement(new { timeZone = Sofia }), DateTimeOffset.UtcNow, null));

        _service = new ScheduleService(
            _factory,
            _launcher,
            new OwnerTimeZoneResolver(_settings, Substitute.For<ILogger<OwnerTimeZoneResolver>>()),
            TimeProvider.System,
            Substitute.For<ILogger<ScheduleService>>());
    }

    public async ValueTask DisposeAsync() => await _scheduler.Shutdown(waitForJobsToComplete: false);

    [Fact]
    public void JobData_RoundTrips_AsStrings()
    {
        var definition = new ScheduleDefinition(ScheduleKind.Workflow, "Digest", null, Guid.NewGuid(), "Household", "Summarise\nthe week", ["a_tool", "b_tool"]);
        var data = new JobDataMap();

        definition.WriteTo(data);

        Assert.All(data.Values, value => Assert.IsType<string>(value));
        Assert.Equivalent(definition, ScheduleDefinition.Read(data));
    }

    [Fact]
    public void JobData_WithoutKind_IsAScriptJob()
    {
        var data = new JobDataMap { [ScheduledJobData.AgentId] = "Agent", [ScheduledJobData.Code] = "print(1)" };

        Assert.Equal(ScheduleKind.Script, ScheduleDefinition.ReadKind(data));
        Assert.Null(ScheduleDefinition.Read(data));
    }

    [Theory]
    [InlineData("A new name", "Brief me", true)]
    [InlineData("Briefing", "Brief me about everything", false)]
    public void PreApprovals_AreKept_OnlyWhenWhatRunsIsUnchanged(string name, string prompt, bool kept)
    {
        var previous = new ScheduleDefinition(ScheduleKind.Agent, "Briefing", "TimelinePlanner", null, "TimelinePlanner", "Brief me", ["execute_code_sandbox"]);
        var edit = previous with { Name = name, Prompt = prompt, PreApprovedTools = [] };

        var result = edit.CarryPreApprovalsFrom(previous);

        Assert.Equal(kept ? ["execute_code_sandbox"] : [], result.PreApprovedTools);
    }

    [Fact]
    public void PreApprovals_AreCleared_WhenTheAgentChanges()
    {
        var previous = new ScheduleDefinition(ScheduleKind.Agent, "Briefing", "TimelinePlanner", null, "TimelinePlanner", "Brief me", ["execute_code_sandbox"]);

        var result = (previous with { AgentName = "GeneralAssistant", PreApprovedTools = [] }).CarryPreApprovalsFrom(previous);

        Assert.Empty(result.PreApprovedTools);
    }

    [Fact]
    public async Task Create_SchedulesAnAgentRunJob_InTheOwnersGroupAndTimeZone()
    {
        var result = await _service.CreateAsync(Alice, AgentRequest(preApproved: ["EXECUTE_CODE_SANDBOX"]), Ct);

        Assert.True(result.IsSuccess, result.Error);
        var key = Assert.Single(await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(SchedulerDefaults.GroupFor(Alice)), Ct));
        var detail = await _scheduler.GetJobDetail(key, Ct);
        Assert.Equal(typeof(AgentRunJob), detail!.JobType);
        Assert.All(detail.JobDataMap.Values, value => Assert.IsType<string>(value));

        var definition = ScheduleDefinition.Read(detail.JobDataMap)!;
        Assert.Equal("TimelinePlanner", definition.AgentName);
        Assert.Equal(["execute_code_sandbox"], definition.PreApprovedTools);

        var trigger = Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(await _scheduler.GetTriggersOfJob(key, Ct)));
        Assert.Equal(Sofia, trigger.TimeZone.Id);
        Assert.Equal(Daily0730, trigger.CronExpressionString);
        Assert.Equal(MisfireInstruction.CronTrigger.FireOnceNow, trigger.MisfireInstruction);
        Assert.Equal(Sofia, result.Value!.TimeZone);
    }

    [Fact]
    public async Task Create_RejectsAnAgentTheOwnerDoesNotHave()
    {
        var result = await _service.CreateAsync(Alice, AgentRequest() with { AgentName = "Nobody" }, Ct);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct));
    }

    [Fact]
    public async Task Create_RejectsAPreApprovalForAToolThatIsNotGated()
    {
        var result = await _service.CreateAsync(Alice, AgentRequest(preApproved: ["get_timeline_overview"]), Ct);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains("get_timeline_overview", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("* * * * * ?")]
    [InlineData("0 * * * *")]
    public async Task Create_RejectsTooFrequentOrInvalidCron(string cron)
    {
        var result = await _service.CreateAsync(Alice, AgentRequest() with { CronExpression = cron }, Ct);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Create_RejectsScriptSchedules()
    {
        var result = await _service.CreateAsync(Alice, AgentRequest() with { Kind = ScheduleKind.Script }, Ct);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Create_EnforcesThePerUserLimit()
    {
        for (var i = 0; i < SchedulerDefaults.MaxJobsPerUser; i++)
        {
            Assert.True((await _service.CreateAsync(Alice, AgentRequest(), Ct)).IsSuccess);
        }

        var result = await _service.CreateAsync(Alice, AgentRequest(), Ct);

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Edit_OfThePrompt_ClearsPreApprovals_ButARenameKeepsThem()
    {
        var key = await CreateAsync(AgentRequest(preApproved: ["execute_code_sandbox"]));

        var renamed = await _service.UpdateAsync(key, AgentRequest() with { Name = "Renamed" }, Ct);
        Assert.Equal(["execute_code_sandbox"], renamed.Value!.PreApprovedTools);

        var reprompted = await _service.UpdateAsync(key, AgentRequest() with { Name = "Renamed", Prompt = "Something else" }, Ct);
        Assert.Empty(reprompted.Value!.PreApprovedTools!);
    }

    [Fact]
    public async Task Edit_ChangesTheCadence()
    {
        var key = await CreateAsync(AgentRequest());

        var result = await _service.UpdateAsync(key, AgentRequest() with { CronExpression = Weekly }, Ct);

        Assert.Equal(Weekly, result.Value!.CronExpression);
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(await _scheduler.GetTriggersOfJob(key, Ct)));
        Assert.Equal(Weekly, trigger.CronExpressionString);
    }

    [Fact]
    public async Task Pause_StopsFires_AndResumeRestoresThem()
    {
        var key = await CreateAsync(AgentRequest());

        var paused = await _service.PauseAsync(key, Ct);
        Assert.True(paused.Value!.Paused);
        Assert.Null(paused.Value.NextRun);

        var editedWhilePaused = await _service.UpdateAsync(key, AgentRequest() with { CronExpression = Weekly }, Ct);
        Assert.True(editedWhilePaused.Value!.Paused);

        var resumed = await _service.ResumeAsync(key, Ct);
        Assert.False(resumed.Value!.Paused);
        Assert.NotNull(resumed.Value.NextRun);
        Assert.Equal(Weekly, resumed.Value.CronExpression);
    }

    [Fact]
    public async Task RunRecorder_KeepsTheLastRun_OnTheSchedule()
    {
        var key = await CreateAsync(AgentRequest());
        var runId = Guid.NewGuid();
        var recorder = new ScheduleRunRecorder(_factory, Substitute.For<ILogger<ScheduleRunRecorder>>());

        await recorder.OnRunCompletedAsync(new UnattendedRunCompletion(key.Name, Alice, runId, UnattendedRunStatus.TimedOut, DateTimeOffset.UtcNow), Ct);

        var listed = Assert.Single(await _service.ListAsync(GroupMatcher<JobKey>.AnyGroup(), Ct));
        Assert.Equal(runId, listed.LastRun!.RunId);
        Assert.Equal(nameof(UnattendedRunStatus.TimedOut), listed.LastRun.Status);
        Assert.NotNull(ScheduleDefinition.Read((await _scheduler.GetJobDetail(key, Ct))!.JobDataMap));
    }

    [Fact]
    public async Task AgentRunJob_LaunchesTheScheduleAsItsOwner()
    {
        var key = await CreateAsync(AgentRequest(preApproved: ["execute_code_sandbox"]));
        var detail = await _scheduler.GetJobDetail(key, Ct);
        var context = Substitute.For<IJobExecutionContext>();
        context.JobDetail.Returns(detail);
        context.MergedJobDataMap.Returns(detail!.JobDataMap);
        _launcher.LaunchAsync(Arg.Any<UnattendedRunRequest>(), Arg.Any<CancellationToken>()).Returns(UnattendedRunLaunch.SkippedStillRunning);

        await new AgentRunJob(_launcher, Substitute.For<ILogger<AgentRunJob>>()).Execute(context);

        await _launcher.Received(1).LaunchAsync(
            Arg.Is<UnattendedRunRequest>(r => r.ScheduleId == key.Name
                && r.OwnerId == Alice
                && r.Kind == UnattendedRunKind.Agent
                && r.AgentName == "TimelinePlanner"
                && r.Prompt == "Brief me"
                && r.PreApprovedTools.SequenceEqual(new[] { "execute_code_sandbox" })),
            Arg.Any<CancellationToken>());
    }

    private static ToolDescriptor Gated(string name) => new(name, string.Empty, "Code", "Execute", "High", RequiresApproval: true);

    private static SaveScheduleRequest AgentRequest(IReadOnlyList<string>? preApproved = null) =>
        new("Briefing", ScheduleKind.Agent, Daily0730, AgentName: "timelineplanner", Prompt: "Brief me", PreApprovedTools: preApproved);

    private async Task<JobKey> CreateAsync(SaveScheduleRequest request)
    {
        var result = await _service.CreateAsync(Alice, request, Ct);
        Assert.True(result.IsSuccess, result.Error);
        return new JobKey(result.Value!.JobId, SchedulerDefaults.GroupFor(Alice));
    }
}
