using System.Collections.Specialized;
using FluentAssertions;
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

        policy.RequiresApproval.Should().BeTrue();
        policy.RiskLevel.Should().Be(ToolRiskLevel.High);
        policy.Resource.Should().Be(ToolResource.Code, "Sentinel must inspect the scheduled script like any code execution");
        CreateTool(Alice).CreateFunction().Should().BeOfType<ApprovalRequiredAIFunction>();
    }

    [Fact]
    public async Task Tool_SchedulesUnderOwnersGroup_WithAgentFromRunContext_InUtc()
    {
        var result = await ScheduleAs(Alice, "ResearchAnalyst", code: "print(1)", language: ScheduledJobLanguage.NodeJs);

        result.Should().StartWith("Success");
        var key = (await JobsOf(Alice)).Should().ContainSingle().Subject;

        var detail = await _scheduler.GetJobDetail(key, Ct);
        detail!.JobType.Should().Be<ExecuteJob>();
        detail.JobDataMap.GetString(ScheduledJobData.AgentId).Should().Be("ResearchAnalyst");
        detail.JobDataMap.GetString(ScheduledJobData.Language).Should().Be("NodeJs");
        detail.JobDataMap.GetString(ScheduledJobData.Code).Should().Be("print(1)");
        detail.JobDataMap.Values.Should().AllBeOfType<string>("the persistent store runs with UseProperties = true");

        var trigger = (await _scheduler.GetTriggersOfJob(key, Ct)).Should().ContainSingle().Subject.As<ICronTrigger>();
        trigger.TimeZone.Should().Be(TimeZoneInfo.Utc);
        trigger.MisfireInstruction.Should().Be(MisfireInstruction.CronTrigger.FireOnceNow);
    }

    [Fact]
    public void Tool_OffersNoOwnerOrAgentArgument_ToTheModel()
    {
        var properties = CreateTool(Alice).CreateFunction().JsonSchema.GetProperty("properties");

        properties.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("jobName", "cronExpression", "code", "language");
    }

    [Fact]
    public async Task Tool_OutsideAnAgentRun_AttributesTheJobToAGenericAgent()
    {
        (await CreateTool(Alice).ScheduleAsync("Report", Hourly, "print(1)", ct: Ct)).Should().StartWith("Success");

        var detail = await _scheduler.GetJobDetail((await JobsOf(Alice)).Single(), Ct);
        detail!.JobDataMap.GetString(ScheduledJobData.AgentId).Should().Be("Agent");
    }

    [Fact]
    public async Task Tool_WithoutUser_FailsClosed()
    {
        var result = await CreateTool(userId: null).ScheduleAsync("Report", Hourly, "print('hi')", ct: Ct);

        result.Should().StartWith("Error").And.Contain("signed-in user");
        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("* * * * * ?")]          // every second
    [InlineData("0/30 * * * * ?")]       // every 30 seconds
    [InlineData("0,10 0 * * * ?")]       // two fires 10 s apart once an hour: uneven gaps
    public async Task Tool_RejectsSchedulesBelowMinimumInterval(string cron)
    {
        var result = await CreateTool(Alice).ScheduleAsync("Report", cron, "print('hi')", ct: Ct);

        result.Should().StartWith("Error").And.Contain("at least");
        (await JobsOf(Alice)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("0 * * * *")]            // 5-field crontab
    [InlineData("0 0 0 1 1 ? 2000")]     // only in the past
    public async Task Tool_RejectsInvalidOrNeverFiringExpressions(string cron)
    {
        var result = await CreateTool(Alice).ScheduleAsync("Report", cron, "print('hi')", ct: Ct);

        result.Should().StartWith("Error");
        (await JobsOf(Alice)).Should().BeEmpty();
    }

    [Fact]
    public async Task Tool_EnforcesPerUserQuota()
    {
        var tool = CreateTool(Alice);
        for (var i = 0; i < SchedulerDefaults.MaxJobsPerUser; i++)
        {
            (await tool.ScheduleAsync($"job-{i}", Hourly, "print('hi')", ct: Ct)).Should().StartWith("Success");
        }

        var result = await tool.ScheduleAsync("one-too-many", Hourly, "print('hi')", ct: Ct);

        result.Should().StartWith("Error").And.Contain("limit");
        (await JobsOf(Alice)).Should().HaveCount(SchedulerDefaults.MaxJobsPerUser);
    }

    [Fact]
    public async Task Controller_ListsOnlyTheCallersJobs_ButEverythingForSovereign()
    {
        await ScheduleAs(Alice, "AliceAgent", jobName: "alice-job");
        await ScheduleAs(Bob, "BobAgent", jobName: "bob-job");

        var aliceView = await CreateController(Alice).GetAllActiveJobsAsync(Ct);
        var sovereignView = await CreateController(Guid.NewGuid(), sovereign: true).GetAllActiveJobsAsync(Ct);

        Jobs(aliceView).Should().ContainSingle()
            .Which.Should().Match<ScheduledJobResponse>(j => j.JobName == "alice-job" && j.AgentId == "AliceAgent" && j.CronExpression == Hourly);
        Jobs(sovereignView).Select(j => j.JobName).Should().BeEquivalentTo("alice-job", "bob-job");
    }

    [Fact]
    public async Task Controller_CannotDeleteAnotherUsersJob()
    {
        await ScheduleAs(Bob, "BobAgent");
        var bobJob = (await JobsOf(Bob)).Single();

        var result = await CreateController(Alice).DeleteScheduledJobAsync("BobAgent", bobJob.Name, Ct);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
        (await _scheduler.CheckExists(bobJob, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Controller_DeletesOwnJob_AndSovereignDeletesAnyJob()
    {
        await ScheduleAs(Alice, "AliceAgent");
        await ScheduleAs(Bob, "BobAgent");
        var aliceJob = (await JobsOf(Alice)).Single();
        var bobJob = (await JobsOf(Bob)).Single();

        (await CreateController(Alice).DeleteScheduledJobAsync("AliceAgent", aliceJob.Name, Ct)).Should().BeOfType<NoContentResult>();
        (await CreateController(Guid.NewGuid(), sovereign: true).DeleteScheduledJobAsync("BobAgent", bobJob.Name, Ct)).Should().BeOfType<NoContentResult>();

        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct)).Should().BeEmpty();
    }

    private static IReadOnlyList<ScheduledJobResponse> Jobs(ActionResult<IReadOnlyList<ScheduledJobResponse>> result) =>
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IReadOnlyList<ScheduledJobResponse>>().Subject;
}
