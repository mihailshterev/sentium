using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Features.Inbox;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Features.Overview;
using Sentium.Tempus.Features.Reminders;
using Sentium.Tempus.Features.Series;
using Sentium.Tempus.Features.Tasks;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Tempus;

/// <summary>
/// Calendar Events, Series and Reminders end to end. Dates are relative to the real UTC today, because "past",
/// overdue and the rolling Series horizon all are; the workers are called directly with an explicit "now" (the test
/// factory does not run hosted services). The household time zone is the default, UTC.
/// </summary>
public sealed class TempusEntriesTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Tasks = "/api/tempus/tasks";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ITimelineAgentAccess Agents => factory.Services.GetRequiredService<ITimelineAgentAccess>();

    [Fact]
    public async Task AnEventYesterday_IsPast_WhileATaskYesterday_IsOverdueUntilTicked()
    {
        var client = ClientFor(Guid.NewGuid());
        var yesterday = Today.AddDays(-1);

        var calendarEvent = await CreateAsync(client, Event("Concert", yesterday));
        var task = await CreateAsync(client, Task("Pay rent", yesterday));

        var month = await MonthAsync(client, yesterday.Year, yesterday.Month);
        month.Tasks.Single(t => t.Id == calendarEvent.Id).IsPast.Should().BeTrue();
        month.Tasks.Single(t => t.Id == task.Id).IsPast.Should().BeFalse();

        var upcoming = await UpcomingAsync(client);
        upcoming.Overdue.Select(t => t.Id).Should().Equal(task.Id);

        var tick = await client.PutAsJsonAsync($"{Tasks}/{task.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        tick.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UpcomingAsync(client)).Overdue.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEvent_CannotBeTicked_OrCarryTaskOnlyFields()
    {
        var client = ClientFor(Guid.NewGuid());
        var calendarEvent = await CreateAsync(client, Event("Wedding", Today.AddDays(3)));

        var tick = await client.PutAsJsonAsync($"{Tasks}/{calendarEvent.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        tick.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withPriority = await client.PostAsJsonAsync(Tasks, Event("Party", Today) with { Priority = TimelineTaskPriority.High }, Ct);
        withPriority.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task APinnedEvent_IsPastOnlyOnceItsMonthIsOver()
    {
        var client = ClientFor(Guid.NewGuid());
        var lastMonth = Today.AddMonths(-1);

        var past = await CreateAsync(client, Event("Holiday", null, lastMonth.Year, lastMonth.Month));
        var current = await CreateAsync(client, Event("Festival", null, Today.Year, Today.Month));

        (await MonthAsync(client, lastMonth.Year, lastMonth.Month)).Tasks.Single(t => t.Id == past.Id).IsPast.Should().BeTrue();
        (await MonthAsync(client, Today.Year, Today.Month)).Tasks.Single(t => t.Id == current.Id).IsPast.Should().BeFalse();
    }

    [Fact]
    public async Task Events_DoNotChangeTheWorkload_AndAreCountedSeparately()
    {
        var client = ClientFor(Guid.NewGuid());
        var day = Today.AddDays(2);
        await CreateAsync(client, Task("Plan trip", day));

        var before = (await YearAsync(client, day.Year)).Months.Single(m => m.Month == day.Month);
        await CreateAsync(client, Event("Flight", day, endDate: day.AddDays(1)));
        var after = await YearAsync(client, day.Year);
        var month = after.Months.Single(m => m.Month == day.Month);

        month.TaskCount.Should().Be(before.TaskCount);
        month.OpenCount.Should().Be(before.OpenCount);
        month.EventCount.Should().Be(1);
        after.Days.Single(d => d.Date == day).Should().Match<DaySummaryDto>(d => d.Open == 1 && d.Events == 1);
    }

    [Fact]
    public async Task AWeeklyTask_ShowsAYearOfOccurrences_TickingOneLeavesTheOthers_AndDeleteFollowingKeepsTickedOnes()
    {
        var client = ClientFor(Guid.NewGuid());
        var first = await CreateAsync(client, Task("Water plants", Today.AddDays(1)) with { Repeat = Weekly() });

        first.SeriesId.Should().NotBeNull();
        first.Repeat!.Frequency.Should().Be(TimelineRepeatFrequency.Weekly);

        var occurrences = await OccurrencesAsync(client, first.SeriesId!.Value);
        occurrences.Should().HaveCountGreaterThanOrEqualTo(52);
        occurrences[^1].Date.Should().BeOnOrAfter(Today.AddMonths(12).AddDays(-7));

        var third = occurrences[2];
        var sixth = occurrences[5];
        await client.PutAsJsonAsync($"{Tasks}/{sixth.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        (await OccurrencesAsync(client, first.SeriesId.Value)).Count(o => o.Status == TimelineTaskStatus.Done).Should().Be(1);

        var delete = await client.DeleteAsync($"{Tasks}/{third.Id}?scope=Following", Ct);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var left = await OccurrencesAsync(client, first.SeriesId.Value);
        left.Select(o => o.Id).Should().Equal(occurrences[0].Id, occurrences[1].Id, sixth.Id);
    }

    [Fact]
    public async Task DeletingOneOccurrence_KeepsTheMaterialiserFromRecreatingIt()
    {
        var client = ClientFor(Guid.NewGuid());
        var first = await CreateAsync(client, Task("Gym", Today.AddDays(1)) with { Repeat = Weekly() });
        var second = (await OccurrencesAsync(client, first.SeriesId!.Value))[1];

        (await client.DeleteAsync($"{Tasks}/{second.Id}?scope=One", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await MaterializeAsync(DateTimeOffset.UtcNow.AddDays(10));

        (await OccurrencesAsync(client, first.SeriesId.Value)).Should().NotContain(o => o.OccurrenceDate == second.OccurrenceDate);
    }

    [Fact]
    public async Task EditingThisAndFollowing_EndsTheOldSeries_AndStartsANewOne()
    {
        var client = ClientFor(Guid.NewGuid());
        var first = await CreateAsync(client, Task("Standup", Today.AddDays(1)) with { Repeat = Weekly() });
        var occurrences = await OccurrencesAsync(client, first.SeriesId!.Value);
        var fourth = occurrences[3];

        var renamed = Task("Team sync", fourth.Date) with { Repeat = Weekly() };
        var response = await client.PutAsJsonAsync($"{Tasks}/{fourth.Id}?scope=Following", renamed, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var edited = (await response.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;

        edited.SeriesId.Should().NotBe(first.SeriesId!.Value);
        (await OccurrencesAsync(client, first.SeriesId.Value)).Select(o => o.Title).Should().Equal("Standup", "Standup", "Standup");

        var renamedOccurrences = await OccurrencesAsync(client, edited.SeriesId!.Value);
        renamedOccurrences.Should().HaveCountGreaterThan(40).And.OnlyContain(o => o.Title == "Team sync");
        renamedOccurrences[0].Id.Should().Be(fourth.Id);
    }

    [Fact]
    public async Task EditingThisOne_DetachesTheOccurrence()
    {
        var client = ClientFor(Guid.NewGuid());
        var first = await CreateAsync(client, Task("Laundry", Today.AddDays(1)) with { Repeat = Weekly() });
        var second = (await OccurrencesAsync(client, first.SeriesId!.Value))[1];

        var response = await client.PutAsJsonAsync($"{Tasks}/{second.Id}?scope=One", Task("Laundry + ironing", second.Date), Ct);
        var edited = (await response.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;

        edited.IsDetached.Should().BeTrue();
        edited.SeriesId.Should().Be(first.SeriesId);
        (await OccurrencesAsync(client, first.SeriesId.Value)).Count(o => o.Title == "Laundry").Should().BeGreaterThan(40);
    }

    [Fact]
    public async Task TheMaterialiser_IsIdempotent_AndExtendsTheHorizon()
    {
        var client = ClientFor(Guid.NewGuid());
        var first = await CreateAsync(client, Task("Bins", Today.AddDays(1)) with { Repeat = Weekly() });
        var count = (await OccurrencesAsync(client, first.SeriesId!.Value)).Count;

        await MaterializeAsync(DateTimeOffset.UtcNow);
        await MaterializeAsync(DateTimeOffset.UtcNow);
        (await OccurrencesAsync(client, first.SeriesId.Value)).Should().HaveCount(count);

        await MaterializeAsync(DateTimeOffset.UtcNow.AddDays(14));
        (await OccurrencesAsync(client, first.SeriesId.Value)).Should().HaveCount(count + 2);
    }

    [Fact]
    public async Task TheMaterialiser_CreatesOccurrencesOnlyForTheSeriesOwner()
    {
        var owner = ClientFor(Guid.NewGuid());
        var other = ClientFor(Guid.NewGuid());
        var first = await CreateAsync(owner, Task("Owner's chore", Today.AddDays(1)) with { Repeat = Weekly() });

        await MaterializeAsync(DateTimeOffset.UtcNow.AddDays(14));

        var month = await MonthAsync(other, first.Year, first.Month);
        month.Tasks.Should().NotContain(t => t.SeriesId == first.SeriesId);
        (await other.GetAsync($"{Tasks}/{first.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ADayBeforeReminder_SendsExactlyOneNotification_AtDayStart_ToItsOwnerOnly()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var bystander = ClientFor(Guid.NewGuid());
        var day = Today.AddDays(5);

        var task = await CreateAsync(client, Task("Dentist", day) with { ReminderOffsetMinutes = ReminderSchedule.DayBefore });
        var dueAt = new DateTimeOffset(day.AddDays(-1).ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero);

        await SweepAsync(dueAt.AddMinutes(-1));
        (await ReminderInboxAsync(client)).Should().BeEmpty();

        await SweepAsync(dueAt);
        await SweepAsync(dueAt.AddMinutes(5));

        var reminder = (await ReminderInboxAsync(client)).Should().ContainSingle().Subject;
        reminder.Title.Should().Be("Dentist");
        reminder.Occurrences.Should().Be(1);
        reminder.Link.Should().Contain($"day={day:yyyy-MM-dd}");
        (await ReminderInboxAsync(bystander)).Should().BeEmpty();
        task.ReminderOffsetMinutes.Should().Be(ReminderSchedule.DayBefore);
    }

    [Fact]
    public async Task ATaskTickedBeforeItsReminder_NeverFires()
    {
        var client = ClientFor(Guid.NewGuid());
        var day = Today.AddDays(6);
        var task = await CreateAsync(client, Task("Renew passport", day) with { ReminderOffsetMinutes = ReminderSchedule.DayBefore });

        await client.PutAsJsonAsync($"{Tasks}/{task.Id}/status", new SetTimelineTaskStatusRequest(TimelineTaskStatus.Done), Ct);
        await SweepAsync(new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero));

        (await ReminderInboxAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task AReminderForAnEntryAlreadyPast_IsDropped()
    {
        var client = ClientFor(Guid.NewGuid());
        var day = Today.AddDays(7);
        await CreateAsync(client, Event("Recital", day) with { ReminderOffsetMinutes = ReminderSchedule.OnTheDay });

        // The system was off from before the Reminder until after the event.
        await SweepAsync(new DateTimeOffset(day.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        (await ReminderInboxAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task AReminder_IsClaimedOnce_UnderTwoConcurrentSweeps()
    {
        var client = ClientFor(Guid.NewGuid());
        var day = Today.AddDays(8);
        await CreateAsync(client, Task("Call grandma", day) with { StartTime = new TimeOnly(17, 0), ReminderOffsetMinutes = 30 });
        var now = new DateTimeOffset(day.ToDateTime(new TimeOnly(16, 45)), TimeSpan.Zero);

        // Other tests' Reminders may fire too; only this user's Inbox is checked for the count.
        var sent = await System.Threading.Tasks.Task.WhenAll(SweepAsync(now), SweepAsync(now));

        sent.Sum().Should().BeGreaterThanOrEqualTo(1);
        (await ReminderInboxAsync(client)).Should().ContainSingle().Which.Occurrences.Should().Be(1);
    }

    [Fact]
    public async Task ChangingTheUsersTimeZone_RecomputesUnsentReminders()
    {
        var client = ClientFor(Guid.NewGuid());
        var day = Today.AddDays(9);
        await CreateAsync(client, Task("Vet", day) with { ReminderOffsetMinutes = ReminderSchedule.OnTheDay });

        var put = await client.PutAsJsonAsync("/api/registry/settings/calendar-user", new { timeZone = "Europe/Sofia" }, Ct);
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync(Ct));

        // 08:00 in Sofia is 05:00 or 06:00 UTC, before the 08:00 UTC the Reminder was first armed for.
        await SweepAsync(new DateTimeOffset(day.ToDateTime(new TimeOnly(6, 0)), TimeSpan.Zero));

        (await ReminderInboxAsync(client)).Should().ContainSingle();
    }

    [Fact]
    public async Task AnAcceptedEventProposal_BecomesACalendarEvent()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var day = Today.AddDays(4);

        var proposal = await Agents.ProposeEventAsync(
            userId,
            "TimelinePlanner",
            new TimelineEventProposal("Maria's birthday", null, day.Year, day.Month, day, new TimeOnly(18, 0), null, ["family"], "She mentioned it."),
            Ct);
        proposal.IsSuccess.Should().BeTrue(proposal.Error);

        var accept = await client.PostAsync($"/api/tempus/proposals/{proposal.Value!.ProposalId}/accept", null, Ct);
        accept.StatusCode.Should().Be(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync(Ct));
        var created = (await accept.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;

        created.Kind.Should().Be(TimelineEntryKind.CalendarEvent);
        created.StartTime.Should().Be(new TimeOnly(18, 0));
        created.CreatedByAgent.Should().Be("TimelinePlanner");

        var listed = await Agents.ListTasksAsync(userId, new TimelineTaskQuery(day, day, Kind: TimelineEntryKind.CalendarEvent), Ct);
        listed.Should().ContainSingle().Which.Kind.Should().Be(TimelineEntryKind.CalendarEvent);
    }

    private static SaveTimelineTaskRequest Task(string title, DateOnly? date)
    {
        var placed = date ?? Today;
        return new SaveTimelineTaskRequest(title, null, placed.Year, placed.Month, date, null);
    }

    private static SaveTimelineTaskRequest Event(string title, DateOnly? date, int? year = null, int? month = null, DateOnly? endDate = null)
    {
        return new SaveTimelineTaskRequest(
            title,
            null,
            year ?? date!.Value.Year,
            month ?? date!.Value.Month,
            date,
            null,
            Kind: TimelineEntryKind.CalendarEvent,
            EndDate: endDate);
    }

    private static TimelineRepeat Weekly() => new(TimelineRepeatFrequency.Weekly, 1, null, null);

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static async Task<TimelineTaskDto> CreateAsync(HttpClient client, SaveTimelineTaskRequest request)
    {
        var response = await client.PostAsJsonAsync(Tasks, request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<TimelineTaskDto>(Ct))!;
    }

    private static async Task<MonthDetailDto> MonthAsync(HttpClient client, int year, int month)
        => (await client.GetFromJsonAsync<MonthDetailDto>($"/api/tempus/years/{year}/months/{month}", Ct))!;

    private static async Task<YearOverviewDto> YearAsync(HttpClient client, int year)
        => (await client.GetFromJsonAsync<YearOverviewDto>($"/api/tempus/years/{year}", Ct))!;

    private static async Task<UpcomingDto> UpcomingAsync(HttpClient client)
        => (await client.GetFromJsonAsync<UpcomingDto>("/api/tempus/upcoming", Ct))!;

    /// <summary>
    /// A Series' occurrences, in date order, read month by month over the coming year.
    /// </summary>
    private static async Task<List<TimelineTaskDto>> OccurrencesAsync(HttpClient client, Guid seriesId)
    {
        var occurrences = new List<TimelineTaskDto>();
        for (var month = Today.AddDays(-Today.Day + 1); month <= Today.AddMonths(13); month = month.AddMonths(1))
        {
            var detail = await MonthAsync(client, month.Year, month.Month);
            occurrences.AddRange(detail.Tasks.Where(t => t.SeriesId == seriesId));
        }

        return [.. occurrences.OrderBy(o => o.OccurrenceDate)];
    }

    private static async Task<List<NotificationDto>> ReminderInboxAsync(HttpClient client)
    {
        var inbox = await client.GetFromJsonAsync<List<NotificationDto>>("/api/nuntius/notifications", Ct);
        return [.. inbox!.Where(n => n.Category == NotificationCategories.TimelineReminder)];
    }

    private async Task<int> SweepAsync(DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return await scope.ServiceProvider.GetRequiredService<TimelineReminderSweep>().RunAsync(now, Ct);
    }

    private async Task MaterializeAsync(DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        await scope.ServiceProvider.GetRequiredService<TimelineSeriesMaterializer>().RunAsync(now, Ct);
    }
}
