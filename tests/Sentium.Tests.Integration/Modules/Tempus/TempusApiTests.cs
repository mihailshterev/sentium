using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Dtos;
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
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
    }

    private static Task<MonthDetailDto?> GetMonthAsync(HttpClient client, int month = Month)
        => client.GetFromJsonAsync<MonthDetailDto>($"/api/tempus/years/{Year}/months/{month}", Ct);

    [Fact]
    public async Task Task_CanBeMovedPinnedAndCompleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(client, "  Book dentist ", Day(12), priority: TimelineTaskPriority.High);

        task.Title.Should().Be("Book dentist");
        task.Tags.Should().Equal("health");
        task.Origin.Should().Be(TimelineAuthorKind.User);

        var moved = await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month, Day(20)), Ct);
        (await moved.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!.Date.Should().Be(Day(20));

        var pinned = await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month + 1, null), Ct);
        var pinnedTask = (await pinned.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
        pinnedTask.Date.Should().BeNull();
        pinnedTask.Month.Should().Be(Month + 1);

        var done = await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        (await done.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!.CompletedAt.Should().NotBeNull();

        (await GetMonthAsync(client, Month + 1))!.Tasks.Should().ContainSingle(t => t.Id == task.Id && t.Status == TimelineTaskStatus.Done);
        (await GetMonthAsync(client))!.Tasks.Should().BeEmpty();

        (await client.DeleteAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Task_OnADayOutsideItsMonth_IsRejected()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/tempus/tasks", new SaveTimelineTaskRequest("Wrong", null, Year, Month, new DateOnly(Year, Month + 1, 1), null), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

        overview!.Months.Should().HaveCount(12);
        var march = overview.Months.Single(m => m.Month == Month);
        march.TaskCount.Should().Be(4);
        march.OpenCount.Should().Be(3);
        march.DoneCount.Should().Be(1);
        march.OverdueCount.Should().Be(0);
        overview.Months.Where(m => m.Month != Month).Should().OnlyContain(m => m.TaskCount == 0);

        // Pinned tasks have no day, so they get no tick.
        overview.Days.Should().Equal(
            new DaySummaryDto(Day(2), Open: 2, Done: 0, Urgent: true),
            new DaySummaryDto(Day(3), Open: 0, Done: 1, Urgent: false));
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
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
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

        upcoming!.Today.Should().Be(today);
        upcoming.Days.Should().Be(7);
        upcoming.OverdueCount.Should().Be(2);
        // Oldest deadline first: the end of the month two months ago comes before three days ago.
        upcoming.Overdue.Select(t => t.Id).Should().Equal(pinnedLate.Id, late.Id);
        // By day, then by priority within the day.
        upcoming.Next.Select(t => t.Id).Should().Equal(todays.Id, soonUrgent.Id, soonLow.Id);

        var other = await ClientFor(Guid.NewGuid()).GetFromJsonAsync<UpcomingDto>("/api/tempus/upcoming", Ct);
        other!.Overdue.Should().BeEmpty();
        other.Next.Should().BeEmpty();
        other.OverdueCount.Should().Be(0);
    }

    [Fact]
    public async Task Timelines_ArePrivate_EvenFromTheSovereign()
    {
        var owner = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(owner, "Private plan", Day(5));

        var other = ClientFor(Guid.NewGuid());
        (await other.GetAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month, Day(6)), Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetMonthAsync(other))!.Tasks.Should().BeEmpty();

        var sovereign = factory.CreateClient();
        (await sovereign.GetAsync($"/api/tempus/tasks/{task.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetMonthAsync(sovereign))!.Tasks.Should().NotContain(t => t.Id == task.Id);
    }

    [Fact]
    public async Task AgentProposal_IsOnlyVisibleToItsUser_AndCanBeAcceptedOnce()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);

        // The facade is a singleton used outside any HTTP request, as agent tools use it.
        var receipt = await Agents.ProposeTaskAsync(userId, "TimelinePlanner",
            new TimelineTaskProposal("Plan spring trip", "Compare trains", Year, Month, Day(14), null, TimelineTaskPriority.High, ["travel"], null, "The 14th is free"), Ct);
        receipt.IsSuccess.Should().BeTrue(receipt.Error);

        var duplicate = await Agents.ProposeTaskAsync(userId, "TimelinePlanner",
            new TimelineTaskProposal("plan spring trip", null, Year, Month, Day(14), null, TimelineTaskPriority.Normal, [], null, null), Ct);
        duplicate.Value!.AlreadyPending.Should().BeTrue();
        duplicate.Value.ProposalId.Should().Be(receipt.Value!.ProposalId);

        (await ClientFor(Guid.NewGuid()).GetFromJsonAsync<List<TimelineProposalDto>>("/api/tempus/proposals", Ct)).Should().BeEmpty();

        var pending = await client.GetFromJsonAsync<List<TimelineProposalDto>>("/api/tempus/proposals", Ct);
        pending.Should().ContainSingle().Which.ProposedBy.Should().Be("TimelinePlanner");
        (await GetMonthAsync(client))!.Proposals.Should().ContainSingle();

        var accepted = await client.PostAsync($"/api/tempus/proposals/{receipt.Value.ProposalId}/accept", null, Ct);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = (await accepted.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
        task.Title.Should().Be("Plan spring trip");
        task.Origin.Should().Be(TimelineAuthorKind.Agent);
        task.CreatedByAgent.Should().Be("TimelinePlanner");

        (await client.PostAsync($"/api/tempus/proposals/{receipt.Value.ProposalId}/accept", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetMonthAsync(client))!.Proposals.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleProposal_IsSuperseded_WhenTheUserMovesTheTask()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var task = await CreateTaskAsync(client, "Car service", Day(10));

        var first = await Agents.ProposeRescheduleAsync(userId, "TimelinePlanner", new TimelineRescheduleProposal(task.Id, Year, Month, Day(17), "Lighter week"), Ct);
        var second = await Agents.ProposeRescheduleAsync(userId, "TimelinePlanner", new TimelineRescheduleProposal(task.Id, Year, Month, Day(18), "Even lighter"), Ct);
        second.Value!.Superseded.Should().Be(1);

        (await client.PostAsync($"/api/tempus/proposals/{first.Value!.ProposalId}/accept", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month, Day(11)), Ct);
        (await client.PostAsync($"/api/tempus/proposals/{second.Value.ProposalId}/accept", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var superseded = await client.GetFromJsonAsync<List<TimelineProposalDto>>("/api/tempus/proposals?status=Superseded", Ct);
        superseded.Should().HaveCount(2);
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
        moved.Month.Should().Be(Month + 1);
        moved.Date.Should().BeNull();
    }

    [Fact]
    public async Task AgentFacade_CannotTouchAnotherUsersTask()
    {
        var owner = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(owner, "Mine", Day(4));

        var intruder = Guid.NewGuid();
        (await Agents.GetTaskAsync(intruder, task.Id, Ct)).Should().BeNull();
        (await Agents.ProposeRescheduleAsync(intruder, "Agent", new TimelineRescheduleProposal(task.Id, Year, Month, Day(5), null), Ct)).Status.Should().Be(ResultStatus.NotFound);
        (await Agents.AddNoteAsync(intruder, "Agent", new TimelineNoteInput(TimelineNoteKind.Note, "hi", task.Id), Ct)).Status.Should().Be(ResultStatus.NotFound);
        (await Agents.ListTasksAsync(intruder, new TimelineTaskQuery(Day(1), Day(31)), Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task AgentNotes_FollowTheirTask_AndCanBeDismissed()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var task = await CreateTaskAsync(client, "Garden", Day(8));

        var note = await Agents.AddNoteAsync(userId, "TimelinePlanner", new TimelineNoteInput(TimelineNoteKind.StatusUpdate, "Seeds ordered", task.Id), Ct);
        note.IsSuccess.Should().BeTrue(note.Error);
        var monthNote = await Agents.AddNoteAsync(userId, "TimelinePlanner", new TimelineNoteInput(TimelineNoteKind.ProgressReport, "March reviewed", Year: Year, Month: Month), Ct);

        await client.PutAsJsonAsync($"/api/tempus/tasks/{task.Id}/schedule", new ScheduleTimelineTaskRequest(Year, Month + 1, Day(8).AddMonths(1)), Ct);

        var april = await GetMonthAsync(client, Month + 1);
        april!.Notes.Should().ContainSingle(n => n.TaskId == task.Id && n.Author == TimelineAuthorKind.Agent && n.Date == Day(8).AddMonths(1));
        april.Tasks.Single().NoteCount.Should().Be(1);

        var detail = await Agents.GetTaskAsync(userId, task.Id, Ct);
        detail!.Notes.Should().ContainSingle().Which.Content.Should().Be("Seeds ordered");

        (await client.DeleteAsync($"/api/tempus/notes/{monthNote.Value!.NoteId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetMonthAsync(client))!.Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task UserNote_OnATask_IsPlacedWithTheTask()
    {
        var client = ClientFor(Guid.NewGuid());
        var task = await CreateTaskAsync(client, "Call grandma", Day(21));

        var response = await client.PostAsJsonAsync("/api/tempus/notes", new CreateTimelineNoteRequest(Year, 1, null, task.Id, "Ask about the recipe"), Ct);

        var note = (await response.Content.ReadFromJsonAsync<TimelineNoteDto>(Ct))!;
        note.Month.Should().Be(Month);
        note.Date.Should().Be(Day(21));
        note.Author.Should().Be(TimelineAuthorKind.User);
    }

    [Fact]
    public async Task Overview_ForAgents_ReportsUpcomingLoad()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await client.PostAsJsonAsync("/api/tempus/tasks", new SaveTimelineTaskRequest("Today's task", null, today.Year, today.Month, today, null), Ct);

        var overview = await Agents.GetOverviewAsync(userId, upcomingDays: 3, ct: Ct);

        overview.Today.Should().Be(today);
        overview.UpcomingDays.Should().HaveCount(3);
        overview.UpcomingDays[0].Open.Should().Be(1);
        overview.Months.Single(m => m.Month == today.Month).Open.Should().Be(1);
    }
}
