using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Overview;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Tasks;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Tempus;

/// <summary>
/// The timeline API end to end. Each test acts as fresh Member users (see <see cref="TestAuthHandler.UserHeader"/>), so
/// tests never see each other's rows - and isolation itself can be asserted, including against the Sovereign.
/// </summary>
public sealed class TempusApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const int Year = 2030;
    private const int Month = 3;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ITimelineAgentAccess Agents => factory.Services.GetRequiredService<ITimelineAgentAccess>();

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static DateOnly Day(int day) => new(Year, Month, day);

    private static async Task<TimelineTaskDto> CreateTaskAsync(HttpClient client, string title, DateOnly? date, int month = Month, TimelineTaskPriority priority = TimelineTaskPriority.Normal)
    {
        var response = await client.PostAsJsonAsync("/api/tempus/tasks", new SaveTimelineTaskRequest(title, null, Year, month, date, null, priority, Tags: [" Health ", "health"]), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
    }

    private static Task<MonthDetailDto?> GetMonthAsync(HttpClient client, int month = Month)
        => client.GetFromJsonAsync<MonthDetailDto>($"/api/tempus/years/{Year}/months/{month}", Ct);

    [Fact]
    public async Task Task_CanBeMovedPinnedAndCompleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(client, "  Book dentist ", Day(12), priority: TimelineTaskPriority.High);

        Assert.Equal("Book dentist", task.Title);
        Assert.Equal(new string[] { "health" }, task.Tags);
        Assert.Equal(TimelineAuthorKind.User, task.Origin);

        var moved = await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month, Day(20)), Ct);
        Assert.Equal(Day(20), (await moved.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!.Date);

        var pinned = await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month + 1, null), Ct);
        var pinnedTask = (await pinned.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
        Assert.Null(pinnedTask.Date);
        Assert.Equal(Month + 1, pinnedTask.Month);

        var done = await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        Assert.NotNull((await done.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!.CompletedAt);

        Assert.Single((await GetMonthAsync(client, Month + 1))!.Tasks, t => t.Id == task.Id && t.Status == TimelineTaskStatus.Done);
        Assert.Empty((await GetMonthAsync(client))!.Tasks);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Task_OnADayOutsideItsMonth_IsRejected()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/tempus/tasks", new SaveTimelineTaskRequest("Wrong", null, Year, Month, new DateOnly(Year, Month + 1, 1), null), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task YearOverview_CountsOpenDoneAndOverdue()
    {
        var client = ClientFor(Guid.NewGuid());
        await CreateTaskAsync(client, "Late", Day(2));
        var done = await CreateTaskAsync(client, "Finished", Day(3));
        await client.PutAsJsonAsync($"/api/tempus/tasks/{done.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        await CreateTaskAsync(client, "Pinned", null);
        await CreateTaskAsync(client, "Big one", Day(2), priority: TimelineTaskPriority.Urgent);

        // The client's "today" is ignored unless it is within a day of UTC, so overdue is relative to the real date:
        // every task in 2030 is in the future.
        var overview = await client.GetFromJsonAsync<YearOverviewDto>($"/api/tempus/years/{Year}?today={Year}-03-10", Ct);

        Assert.Equal(12, overview!.Months.Count);
        var march = overview.Months.Single(m => m.Month == Month);
        Assert.Equal(4, march.TaskCount);
        Assert.Equal(3, march.OpenCount);
        Assert.Equal(1, march.DoneCount);
        Assert.Equal(0, march.OverdueCount);
        Assert.All(overview.Months.Where(m => m.Month != Month), m => Assert.True(m.TaskCount == 0));

        // Pinned tasks have no day, so they get no tick.
        Assert.Equal(new DaySummaryDto[] { new DaySummaryDto(Day(2), Open: 2, Done: 0, Urgent: true, Events: 0), new DaySummaryDto(Day(3), Open: 0, Done: 1, Urgent: false, Events: 0) }, overview.Days);
    }

    [Fact]
    public async Task Upcoming_ListsOverdueAndTheComingDays_ForItsUserOnly()
    {
        var client = ClientFor(Guid.NewGuid());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var twoMonthsAgo = today.AddMonths(-2);

        async Task<TimelineTaskDto> Create(string title, int year, int month, DateOnly? date, TimelineTaskPriority priority = TimelineTaskPriority.Normal)
        {
            var response = await client.PostAsJsonAsync("/api/tempus/tasks", new SaveTimelineTaskRequest(title, null, year, month, date, null, priority), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
            return (await response.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
        }

        var late = await Create("Late", today.AddDays(-3).Year, today.AddDays(-3).Month, today.AddDays(-3));
        var pinnedLate = await Create("Pinned two months ago", twoMonthsAgo.Year, twoMonthsAgo.Month, null);
        var finished = await Create("Finished late", today.AddDays(-2).Year, today.AddDays(-2).Month, today.AddDays(-2));
        await client.PutAsJsonAsync($"/api/tempus/tasks/{finished.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        var soonLow = await Create("Soon, low", today.AddDays(2).Year, today.AddDays(2).Month, today.AddDays(2), TimelineTaskPriority.Low);
        var soonUrgent = await Create("Soon, urgent", today.AddDays(2).Year, today.AddDays(2).Month, today.AddDays(2), TimelineTaskPriority.Urgent);
        var todays = await Create("Today", today.Year, today.Month, today);
        await Create("Far away", today.AddDays(20).Year, today.AddDays(20).Month, today.AddDays(20));

        var upcoming = await client.GetFromJsonAsync<UpcomingDto>("/api/tempus/upcoming?days=7", Ct);

        Assert.Equal(today, upcoming!.Today);
        Assert.Equal(7, upcoming.Days);
        Assert.Equal(2, upcoming.OverdueCount);
        // Oldest deadline first: the end of the month two months ago comes before three days ago.
        Assert.Equal(new Guid[] { pinnedLate.Id, late.Id }, upcoming.Overdue.Select(t => t.Id));
        // By day, then by priority within the day.
        Assert.Equal(new Guid[] { todays.Id, soonUrgent.Id, soonLow.Id }, upcoming.Next.Select(t => t.Id));

        var other = await ClientFor(Guid.NewGuid()).GetFromJsonAsync<UpcomingDto>("/api/tempus/upcoming", Ct);
        Assert.Empty(other!.Overdue);
        Assert.Empty(other.Next);
        Assert.Equal(0, other.OverdueCount);
    }

    [Fact]
    public async Task Timelines_ArePrivate_EvenFromTheSovereign()
    {
        var owner = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(owner, "Private plan", Day(5));

        var other = ClientFor(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month, Day(6)), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode);
        Assert.Empty((await GetMonthAsync(other))!.Tasks);

        var sovereign = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await sovereign.GetAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode);
        Assert.DoesNotContain((await GetMonthAsync(sovereign))!.Tasks, t => t.Id == task.Id);
    }

    [Fact]
    public async Task AgentProposal_IsOnlyVisibleToItsUser_AndCanBeAcceptedOnce()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);

        // The facade is a singleton used outside any HTTP request, as agent tools use it.
        var receipt = await Agents.ProposeTaskAsync(userId, "TimelinePlanner",
            new TimelineTaskProposal("Plan spring trip", "Compare trains", Year, Month, Day(14), null, TimelineTaskPriority.High, ["travel"], null, "The 14th is free"), Ct);
        Assert.True(receipt.IsSuccess, receipt.Error);

        var duplicate = await Agents.ProposeTaskAsync(userId, "TimelinePlanner",
            new TimelineTaskProposal("plan spring trip", null, Year, Month, Day(14), null, TimelineTaskPriority.Normal, [], null, null), Ct);
        Assert.True(duplicate.Value!.AlreadyPending);
        Assert.Equal(receipt.Value!.ProposalId, duplicate.Value.ProposalId);

        Assert.Empty((await ClientFor(Guid.NewGuid()).GetFromJsonAsync<List<TimelineProposalDto>>("/api/tempus/proposals", Ct))!);

        var pending = await client.GetFromJsonAsync<List<TimelineProposalDto>>("/api/tempus/proposals", Ct);
        Assert.Equal("TimelinePlanner", Assert.Single(pending!).ProposedBy);
        Assert.Single((await GetMonthAsync(client))!.Proposals);

        var accepted = await client.PostAsync($"/api/tempus/proposals/{receipt.Value.ProposalId}/accept", null, Ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var task = (await accepted.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
        Assert.Equal("Plan spring trip", task.Title);
        Assert.Equal(TimelineAuthorKind.Agent, task.Origin);
        Assert.Equal("TimelinePlanner", task.CreatedByAgent);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/tempus/proposals/{receipt.Value.ProposalId}/accept", null, Ct)).StatusCode);
        Assert.Empty((await GetMonthAsync(client))!.Proposals);
    }

    [Fact]
    public async Task RescheduleProposal_IsSuperseded_WhenTheUserMovesTheTask()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var task = await CreateTaskAsync(client, "Car service", Day(10));

        var first = await Agents.ProposeRescheduleAsync(userId, "TimelinePlanner", new TimelineRescheduleProposal(task.Id, Year, Month, Day(17), "Lighter week"), Ct);
        var second = await Agents.ProposeRescheduleAsync(userId, "TimelinePlanner", new TimelineRescheduleProposal(task.Id, Year, Month, Day(18), "Even lighter"), Ct);
        Assert.Equal(1, second.Value!.Superseded);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/tempus/proposals/{first.Value!.ProposalId}/accept", null, Ct)).StatusCode);

        await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month, Day(11)), Ct);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/tempus/proposals/{second.Value.ProposalId}/accept", null, Ct)).StatusCode);

        var superseded = await client.GetFromJsonAsync<List<TimelineProposalDto>>("/api/tempus/proposals?status=Superseded", Ct);
        Assert.Equal(2, superseded!.Count);
    }

    [Fact]
    public async Task RescheduleProposal_MovesTheTask_WhenAccepted()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var task = await CreateTaskAsync(client, "Tax return", Day(10));

        var proposal = await Agents.ProposeRescheduleAsync(userId, "TimelinePlanner", new TimelineRescheduleProposal(task.Id, Year, Month + 1, null, "March is full"), Ct);

        var accepted = await client.PostAsync($"/api/tempus/proposals/{proposal.Value!.ProposalId}/accept", null, Ct);
        var moved = (await accepted.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
        Assert.Equal(Month + 1, moved.Month);
        Assert.Null(moved.Date);
    }

    [Fact]
    public async Task AgentFacade_CannotTouchAnotherUsersTask()
    {
        var owner = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(owner, "Mine", Day(4));

        var intruder = Guid.NewGuid();
        Assert.Null(await Agents.GetTaskAsync(intruder, task.Id, Ct));
        Assert.Equal(ResultStatus.NotFound, (await Agents.ProposeRescheduleAsync(intruder, "Agent", new TimelineRescheduleProposal(task.Id, Year, Month, Day(5), null), Ct)).Status);
        Assert.Equal(ResultStatus.NotFound, (await Agents.AddNoteAsync(intruder, "Agent", new TimelineNoteInput(TimelineNoteKind.Note, "hi", task.Id), Ct)).Status);
        Assert.Empty(await Agents.ListTasksAsync(intruder, new TimelineTaskQuery(Day(1), Day(31)), Ct));
    }

    [Fact]
    public async Task AgentNotes_FollowTheirTask_AndCanBeDismissed()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var task = await CreateTaskAsync(client, "Garden", Day(8));

        var note = await Agents.AddNoteAsync(userId, "TimelinePlanner", new TimelineNoteInput(TimelineNoteKind.StatusUpdate, "Seeds ordered", task.Id), Ct);
        Assert.True(note.IsSuccess, note.Error);
        var monthNote = await Agents.AddNoteAsync(userId, "TimelinePlanner", new TimelineNoteInput(TimelineNoteKind.ProgressReport, "March reviewed", Year: Year, Month: Month), Ct);

        await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month + 1, Day(8).AddMonths(1)), Ct);

        var april = await GetMonthAsync(client, Month + 1);
        Assert.Single(april!.Notes, n => n.TaskId == task.Id && n.Author == TimelineAuthorKind.Agent && n.Date == Day(8).AddMonths(1));
        Assert.Equal(1, april.Tasks.Single().NoteCount);

        var detail = await Agents.GetTaskAsync(userId, task.Id, Ct);
        Assert.Equal("Seeds ordered", Assert.Single(detail!.Notes).Content);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tempus/notes/{monthNote.Value!.NoteId}", Ct)).StatusCode);
        Assert.Empty((await GetMonthAsync(client))!.Notes);
    }

    [Fact]
    public async Task UserNote_OnATask_IsPlacedWithTheTask()
    {
        var client = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(client, "Call grandma", Day(21));

        var response = await client.PostAsJsonAsync("/api/tempus/notes", new CreateTimelineNoteRequest(Year, 1, null, task.Id, "Ask about the recipe"), Ct);

        var note = (await response.Content.ReadFromJsonAsync<TimelineNoteDto>(Ct))!;
        Assert.Equal(Month, note.Month);
        Assert.Equal(Day(21), note.Date);
        Assert.Equal(TimelineAuthorKind.User, note.Author);
    }

    [Fact]
    public async Task Overview_ForAgents_ReportsUpcomingLoad()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await client.PostAsJsonAsync("/api/tempus/tasks", new SaveTimelineTaskRequest("Today's task", null, today.Year, today.Month, today, null), Ct);

        var overview = await Agents.GetOverviewAsync(userId, upcomingDays: 3, ct: Ct);

        Assert.Equal(today, overview.Today);
        Assert.Equal(3, overview.UpcomingDays.Count);
        Assert.Equal(1, overview.UpcomingDays[0].Open);
        Assert.Equal(1, overview.Months.Single(m => m.Month == today.Month).Open);
    }
}
