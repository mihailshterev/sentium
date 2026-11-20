using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Contracts;
using Sentium.Scheduling.Features.Jobs;
using Sentium.Tests.Unit.Modules.Praxis.Support;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Scheduling;

/// <summary>
/// Agent scheduling against a real (unstarted, in-memory) Quartz scheduler, so job keys, groups and
/// trigger settings are asserted as Quartz actually stores them.
/// </summary>
public sealed class SchedulingTests : IAsyncLifetime
{
    private const string Hourly = "0 0 * * * ?";

    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly ISchedulerFactory _factory = new StdSchedulerFactory(new NameValueCollection
    {
        ["quartz.scheduler.instanceName"] = $"unit-{Guid.NewGuid():N}",
        ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
    });

    private readonly IPdpContextAccessor _pdp = Substitute.For<IPdpContextAccessor>();
    private IScheduler _scheduler = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _scheduler = await _factory.GetScheduler(Ct);

    public async ValueTask DisposeAsync() => await _scheduler.Shutdown(waitForJobsToComplete: false);

    private ScheduleTaskTool CreateTool(Guid? userId)
    {
        _pdp.UserId.Returns(userId);
        return new ScheduleTaskTool(_factory, _pdp, Substitute.For<ILogger<ScheduleTaskTool>>());
    }

    /// <summary>
    /// Schedules as the named agent would: inside its Agent Framework run, which is where the tool reads the agent from.
    /// </summary>
    private Task<string> ScheduleAs(
        Guid? userId,
        string agentName,
        string cron = Hourly,
        string jobName = "Report",
        string code = "print('hi')",
        ScheduledJobLanguage language = ScheduledJobLanguage.Python)
    {
        var tool = CreateTool(userId);
        return AgentRunScope.RunAsAsync(agentName, () => tool.ScheduleAsync(jobName, cron, code, language, Ct));
    }

    private async Task<IReadOnlyCollection<JobKey>> JobsOf(Guid userId) =>
        await _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(SchedulerDefaults.GroupFor(userId)), Ct);

    private SchedulerController CreateController(Guid? userId, bool sovereign = false)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId);
        user.IsAuthenticated.Returns(userId.HasValue);
        user.IsSovereign.Returns(sovereign);
        return new SchedulerController(_factory, user)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void Tool_RequiresHighRiskApproval_LikeOneShotCodeExecution()
    {
        var policy = ToolPolicyReader.GetPolicy(CreateTool(Alice));

        Assert.True(policy.RequiresApproval);
        Assert.Equal(ToolRiskLevel.High, policy.RiskLevel);
        // Sentinel must inspect the scheduled script like any code execution
        Assert.Equal(ToolResource.Code, policy.Resource);
        Assert.IsType<ApprovalRequiredAIFunction>(CreateTool(Alice).CreateFunction());
    }

    [Fact]
    public async Task Tool_SchedulesUnderOwnersGroup_WithAgentFromRunContext_InUtc()
    {
        var result = await ScheduleAs(Alice, "ResearchAnalyst", code: "print(1)", language: ScheduledJobLanguage.NodeJs);

        Assert.StartsWith("Success", result, StringComparison.Ordinal);
        var key = Assert.Single(await JobsOf(Alice));

        var detail = await _scheduler.GetJobDetail(key, Ct);
        Assert.Equal(typeof(ExecuteJob), detail!.JobType);
        Assert.Equal("ResearchAnalyst", detail.JobDataMap.GetString(ScheduledJobData.AgentId));
        Assert.Equal("NodeJs", detail.JobDataMap.GetString(ScheduledJobData.Language));
        Assert.Equal("print(1)", detail.JobDataMap.GetString(ScheduledJobData.Code));
        // The persistent store runs with UseProperties = true.
        Assert.All(detail.JobDataMap.Values, value => Assert.IsType<string>(value));

        var trigger = Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(await _scheduler.GetTriggersOfJob(key, Ct)));
        Assert.Equal(TimeZoneInfo.Utc, trigger.TimeZone);
        Assert.Equal(MisfireInstruction.CronTrigger.FireOnceNow, trigger.MisfireInstruction);
    }

    [Fact]
    public void Tool_OffersNoOwnerOrAgentArgument_ToTheModel()
    {
        var properties = CreateTool(Alice).CreateFunction().JsonSchema.GetProperty("properties");

        Assert.Equivalent(new string[] { "jobName", "cronExpression", "code", "language" }, properties.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Tool_OutsideAnAgentRun_AttributesTheJobToAGenericAgent()
    {
        Assert.StartsWith("Success", await CreateTool(Alice).ScheduleAsync("Report", Hourly, "print(1)", ct: Ct), StringComparison.Ordinal);

        var detail = await _scheduler.GetJobDetail((await JobsOf(Alice)).Single(), Ct);
        Assert.Equal("Agent", detail!.JobDataMap.GetString(ScheduledJobData.AgentId));
    }

    [Fact]
    public async Task Tool_WithoutUser_FailsClosed()
    {
        var result = await CreateTool(userId: null).ScheduleAsync("Report", Hourly, "print('hi')", ct: Ct);

        Assert.StartsWith("Error", result, StringComparison.Ordinal);
        Assert.Contains("signed-in user", result, StringComparison.Ordinal);
        Assert.Empty(await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct));
    }

    [Theory]
    [InlineData("* * * * * ?")]          // every second
    [InlineData("0/30 * * * * ?")]       // every 30 seconds
    [InlineData("0,10 0 * * * ?")]       // two fires 10 s apart once an hour: uneven gaps
    public async Task Tool_RejectsSchedulesBelowMinimumInterval(string cron)
    {
        var result = await CreateTool(Alice).ScheduleAsync("Report", cron, "print('hi')", ct: Ct);

        Assert.StartsWith("Error", result, StringComparison.Ordinal);
        Assert.Contains("at least", result, StringComparison.Ordinal);
        Assert.Empty(await JobsOf(Alice));
    }

    [Theory]
    [InlineData("0 * * * *")]            // 5-field crontab
    [InlineData("0 0 0 1 1 ? 2000")]     // only in the past
    public async Task Tool_RejectsInvalidOrNeverFiringExpressions(string cron)
    {
        var result = await CreateTool(Alice).ScheduleAsync("Report", cron, "print('hi')", ct: Ct);

        Assert.StartsWith("Error", result, StringComparison.Ordinal);
        Assert.Empty(await JobsOf(Alice));
    }

    [Fact]
    public async Task Tool_EnforcesPerUserQuota()
    {
        var tool = CreateTool(Alice);
        for (var i = 0; i < SchedulerDefaults.MaxJobsPerUser; i++)
        {
            Assert.StartsWith("Success", await tool.ScheduleAsync($"job-{i}", Hourly, "print('hi')", ct: Ct), StringComparison.Ordinal);
        }

        var result = await tool.ScheduleAsync("one-too-many", Hourly, "print('hi')", ct: Ct);

        Assert.StartsWith("Error", result, StringComparison.Ordinal);
        Assert.Contains("limit", result, StringComparison.Ordinal);
        Assert.Equal(SchedulerDefaults.MaxJobsPerUser, (await JobsOf(Alice)).Count);
    }

    [Fact]
    public async Task Controller_ListsOnlyTheCallersJobs_ButEverythingForSovereign()
    {
        await ScheduleAs(Alice, "AliceAgent", jobName: "alice-job");
        await ScheduleAs(Bob, "BobAgent", jobName: "bob-job");

        var aliceView = await CreateController(Alice).GetAllActiveJobsAsync(Ct);
        var sovereignView = await CreateController(Guid.NewGuid(), sovereign: true).GetAllActiveJobsAsync(Ct);

        var aliceJob = Assert.Single(Jobs(aliceView));
        Assert.Equal("alice-job", aliceJob.JobName);
        Assert.Equal("AliceAgent", aliceJob.AgentId);
        Assert.Equal(Hourly, aliceJob.CronExpression);
        Assert.Equivalent(new string[] { "alice-job", "bob-job" }, Jobs(sovereignView).Select(j => j.JobName));
    }

    [Fact]
    public async Task Controller_CannotDeleteAnotherUsersJob()
    {
        await ScheduleAs(Bob, "BobAgent");
        var bobJob = (await JobsOf(Bob)).Single();

        var result = await CreateController(Alice).DeleteScheduledJobAsync("BobAgent", bobJob.Name, Ct);

        Assert.Equal(404, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.True(await _scheduler.CheckExists(bobJob, Ct));
    }

    [Fact]
    public async Task Controller_DeletesOwnJob_AndSovereignDeletesAnyJob()
    {
        await ScheduleAs(Alice, "AliceAgent");
        await ScheduleAs(Bob, "BobAgent");
        var aliceJob = (await JobsOf(Alice)).Single();
        var bobJob = (await JobsOf(Bob)).Single();

        Assert.IsType<NoContentResult>(await CreateController(Alice).DeleteScheduledJobAsync("AliceAgent", aliceJob.Name, Ct));
        Assert.IsType<NoContentResult>(await CreateController(Guid.NewGuid(), sovereign: true).DeleteScheduledJobAsync("BobAgent", bobJob.Name, Ct));

        Assert.Empty(await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct));
    }

    private static IReadOnlyList<ScheduledJobResponse> Jobs(ActionResult<IReadOnlyList<ScheduledJobResponse>> result)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<ScheduledJobResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }
}
