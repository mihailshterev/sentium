using System.Collections.Specialized;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Controllers;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Features.Scheduling;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Tools.Attributes;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

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

    private ScheduleTaskTool CreateTool(Guid? userId, string agentName = "ResearchAnalyst")
    {
        _pdp.UserId.Returns(userId);
        _pdp.AgentName.Returns(agentName);
        return new ScheduleTaskTool(_factory, _pdp, Substitute.For<ILogger<ScheduleTaskTool>>());
    }

    private static string Args(string cron, string code = "print('hi')", string jobName = "Report") =>
        $$"""{"JobName":"{{jobName}}","CronExpression":"{{cron}}","Code":"{{code}}","Language":"Python"}""";

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
    }

    [Fact]
    public async Task Tool_SchedulesUnderOwnersGroup_WithAgentFromRunContext_InUtc()
    {
        var result = await CreateTool(Alice).ExecuteAsync(
            """{"AgentId":"spoofed-agent","JobName":"Report","CronExpression":"0 0 * * * ?","Code":"print(1)","Language":"NodeJs"}""", Ct);

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
    public async Task Tool_WithoutUser_FailsClosed()
    {
        var result = await CreateTool(userId: null).ExecuteAsync(Args(Hourly), Ct);

        result.Should().StartWith("Error").And.Contain("signed-in user");
        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("* * * * * ?")]          // every second
    [InlineData("0/30 * * * * ?")]       // every 30 seconds
    [InlineData("0,10 0 * * * ?")]       // two fires 10 s apart once an hour: uneven gaps
    public async Task Tool_RejectsSchedulesBelowMinimumInterval(string cron)
    {
        var result = await CreateTool(Alice).ExecuteAsync(Args(cron), Ct);

        result.Should().StartWith("Error").And.Contain("at least");
        (await JobsOf(Alice)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("0 * * * *")]            // 5-field crontab
    [InlineData("0 0 0 1 1 ? 2000")]     // only in the past
    public async Task Tool_RejectsInvalidOrNeverFiringExpressions(string cron)
    {
        var result = await CreateTool(Alice).ExecuteAsync(Args(cron), Ct);

        result.Should().StartWith("Error");
        (await JobsOf(Alice)).Should().BeEmpty();
    }

    [Fact]
    public async Task Tool_EnforcesPerUserQuota()
    {
        var tool = CreateTool(Alice);
        for (var i = 0; i < SchedulerDefaults.MaxJobsPerUser; i++)
        {
            (await tool.ExecuteAsync(Args(Hourly, jobName: $"job-{i}"), Ct)).Should().StartWith("Success");
        }

        var result = await tool.ExecuteAsync(Args(Hourly, jobName: "one-too-many"), Ct);

        result.Should().StartWith("Error").And.Contain("limit");
        (await JobsOf(Alice)).Should().HaveCount(SchedulerDefaults.MaxJobsPerUser);
    }

    [Fact]
    public async Task Controller_ListsOnlyTheCallersJobs_ButEverythingForSovereign()
    {
        await CreateTool(Alice, "AliceAgent").ExecuteAsync(Args(Hourly, jobName: "alice-job"), Ct);
        await CreateTool(Bob, "BobAgent").ExecuteAsync(Args(Hourly, jobName: "bob-job"), Ct);

        var aliceView = await CreateController(Alice).GetAllActiveJobsAsync(Ct);
        var sovereignView = await CreateController(Guid.NewGuid(), sovereign: true).GetAllActiveJobsAsync(Ct);

        Jobs(aliceView).Should().ContainSingle()
            .Which.Should().Match<ScheduledJobResponse>(j => j.JobName == "alice-job" && j.AgentId == "AliceAgent" && j.CronExpression == Hourly);
        Jobs(sovereignView).Select(j => j.JobName).Should().BeEquivalentTo("alice-job", "bob-job");
    }

    [Fact]
    public async Task Controller_CannotDeleteAnotherUsersJob()
    {
        await CreateTool(Bob, "BobAgent").ExecuteAsync(Args(Hourly), Ct);
        var bobJob = (await JobsOf(Bob)).Single();

        var result = await CreateController(Alice).DeleteScheduledJobAsync("BobAgent", bobJob.Name, Ct);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
        (await _scheduler.CheckExists(bobJob, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Controller_DeletesOwnJob_AndSovereignDeletesAnyJob()
    {
        await CreateTool(Alice, "AliceAgent").ExecuteAsync(Args(Hourly), Ct);
        await CreateTool(Bob, "BobAgent").ExecuteAsync(Args(Hourly), Ct);
        var aliceJob = (await JobsOf(Alice)).Single();
        var bobJob = (await JobsOf(Bob)).Single();

        (await CreateController(Alice).DeleteScheduledJobAsync("AliceAgent", aliceJob.Name, Ct)).Should().BeOfType<NoContentResult>();
        (await CreateController(Guid.NewGuid(), sovereign: true).DeleteScheduledJobAsync("BobAgent", bobJob.Name, Ct)).Should().BeOfType<NoContentResult>();

        (await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), Ct)).Should().BeEmpty();
    }

    private static IReadOnlyList<ScheduledJobResponse> Jobs(ActionResult<IReadOnlyList<ScheduledJobResponse>> result) =>
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IReadOnlyList<ScheduledJobResponse>>().Subject;
}
