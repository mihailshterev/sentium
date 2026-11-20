using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Tasks;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Tempus;

public sealed class TempusValidatorTests
{
    private static SaveTimelineTaskRequest Task(
        string title = "Book dentist",
        int year = 2026,
        int month = 11,
        DateOnly? date = null,
        IReadOnlyList<string>? tags = null) => new(title, null, year, month, date, null, TimelineTaskPriority.Normal, TimelineTaskStatus.Todo, tags, Kind: TimelineEntryKind.Task);

    [Fact]
    public void SaveTask_AcceptsADayTask_AndAPinnedTask()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        Assert.True(validator.Validate(Task(date: new DateOnly(2026, 11, 14))).IsValid);
        Assert.True(validator.Validate(Task()).IsValid);
    }

    [Fact]
    public void SaveTask_RejectsADayOutsideTheMonth()
        => Assert.False(new SaveTimelineTaskRequestValidator().Validate(Task(date: new DateOnly(2026, 12, 1))).IsValid);

    [Theory]
    [InlineData("", 2026, 11)]
    [InlineData("Plan", 1999, 11)]
    [InlineData("Plan", 2026, 13)]
    public void SaveTask_RejectsMissingTitles_AndOutOfRangeMonths(string title, int year, int month)
        => Assert.False(new SaveTimelineTaskRequestValidator().Validate(Task(title, year, month)).IsValid);

    [Fact]
    public void SaveTask_RejectsTagsWithCommas_AndTooManyTags()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        Assert.False(validator.Validate(Task(tags: ["health, family"])).IsValid);
        Assert.False(validator.Validate(Task(tags: [.. Enumerable.Range(0, 11).Select(i => $"t{i}")])).IsValid);
    }

    [Fact]
    public void SaveTask_RejectsUnknownEnumValues()
    {
        var request = Task() with { Priority = (TimelineTaskPriority)42 };

        Assert.False(new SaveTimelineTaskRequestValidator().Validate(request).IsValid);
    }

    private static SaveTimelineTaskRequest Event(DateOnly? date = null, DateOnly? endDate = null) => new(
        "Concert", null, 2026, 11, date, null, Priority: null, Status: null, Kind: TimelineEntryKind.CalendarEvent, EndDate: endDate);

    [Fact]
    public void SaveEvent_AcceptsADayEvent_AMultiDayEvent_AndAPinnedEvent()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        Assert.True(validator.Validate(Event(new DateOnly(2026, 11, 14))).IsValid);
        Assert.True(validator.Validate(Event(new DateOnly(2026, 11, 14), new DateOnly(2026, 12, 2))).IsValid);
        Assert.True(validator.Validate(Event()).IsValid);
    }

    [Fact]
    public void SaveEntry_WithoutAKind_LeavesTheKindRulesToTheStoredEntry()
    {
        var update = Event(new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 16)) with { Kind = null };

        Assert.True(new SaveTimelineTaskRequestValidator().Validate(update).IsValid);
    }

    [Fact]
    public void SaveEvent_RejectsTaskOnlyFields()
    {
        var validator = new SaveTimelineTaskRequestValidator();
        var day = new DateOnly(2026, 11, 14);

        Assert.False(validator.Validate(Event(day) with { Status = TimelineTaskStatus.Done }).IsValid);
        Assert.False(validator.Validate(Event(day) with { Priority = TimelineTaskPriority.High }).IsValid);
        Assert.False(validator.Validate(Event(day) with { DueDate = day }).IsValid);
        Assert.False(validator.Validate(Event(day) with { AssignedAgent = "Planner" }).IsValid);
    }

    [Fact]
    public void SaveEntry_RejectsAnEndDayBeforeTheDay_OrWithoutADay_OrOnATask()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        Assert.False(validator.Validate(Event(new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 13))).IsValid);
        Assert.False(validator.Validate(Event(null, new DateOnly(2026, 11, 13))).IsValid);
        Assert.False(validator.Validate(Task(date: new DateOnly(2026, 11, 14)) with { EndDate = new DateOnly(2026, 11, 15) }).IsValid);
    }

    [Fact]
    public void SaveEntry_RejectsATimeWithoutADay()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        Assert.True(validator.Validate(Task(date: new DateOnly(2026, 11, 14)) with { StartTime = new TimeOnly(9, 0) }).IsValid);
        Assert.False(validator.Validate(Task() with { StartTime = new TimeOnly(9, 0) }).IsValid);
    }

    [Theory]
    [InlineData(TimelineRepeatFrequency.Monthly, true)]
    [InlineData(TimelineRepeatFrequency.Yearly, true)]
    [InlineData(TimelineRepeatFrequency.Weekly, false)]
    [InlineData(TimelineRepeatFrequency.Daily, false)]
    public void SaveEntry_APinnedEntry_RepeatsOnlyMonthlyOrYearly(TimelineRepeatFrequency frequency, bool valid)
    {
        var request = Task() with { Repeat = new TimelineRepeat(frequency, 1, null, null) };

        Assert.Equal(valid, new SaveTimelineTaskRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void SaveEntry_RejectsBadRepeatRules()
    {
        var validator = new SaveTimelineTaskRequestValidator();
        var day = new DateOnly(2026, 11, 14);

        Assert.False(validator.Validate(Task(date: day) with { Repeat = new TimelineRepeat(TimelineRepeatFrequency.Weekly, 0, null, null) }).IsValid);
        Assert.False(validator.Validate(Task(date: day) with { Repeat = new TimelineRepeat(TimelineRepeatFrequency.Daily, 1, [DayOfWeek.Monday], null) }).IsValid);
        Assert.False(validator.Validate(Task(date: day) with { Repeat = new TimelineRepeat(TimelineRepeatFrequency.Daily, 1, null, day.AddDays(-1)) }).IsValid);
        Assert.True(validator.Validate(Task(date: day) with { Repeat = new TimelineRepeat(TimelineRepeatFrequency.Weekly, 2, [DayOfWeek.Monday, DayOfWeek.Friday], day.AddMonths(3)) }).IsValid);
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, ReminderSchedule.DayBefore, true)]
    [InlineData(true, ReminderSchedule.WeekBefore, true)]
    [InlineData(true, 90, false)]
    [InlineData(false, 90, true)]
    [InlineData(false, -5, false)]
    [InlineData(false, ReminderSchedule.WeekBefore + 1, false)]
    public void SaveEntry_ReminderOffsets_DependOnWhetherTheEntryHasATime(bool dayOnly, int offset, bool valid)
    {
        var request = Task(date: new DateOnly(2026, 11, 14)) with
        {
            StartTime = dayOnly ? null : new TimeOnly(9, 0),
            ReminderOffsetMinutes = offset
        };

        Assert.Equal(valid, new SaveTimelineTaskRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Schedule_RequiresTheDayInTheTargetMonth()
    {
        var validator = new ScheduleTimelineTaskRequestValidator();

        Assert.True(validator.Validate(new ScheduleTimelineTaskRequest(2026, 11, new DateOnly(2026, 11, 30))).IsValid);
        Assert.True(validator.Validate(new ScheduleTimelineTaskRequest(2026, 11, null)).IsValid);
        Assert.False(validator.Validate(new ScheduleTimelineTaskRequest(2026, 11, new DateOnly(2026, 10, 31))).IsValid);
    }

    [Fact]
    public void Note_RequiresContent()
    {
        var validator = new CreateTimelineNoteRequestValidator();

        Assert.True(validator.Validate(new CreateTimelineNoteRequest(2026, 11, null, null, "Looks good")).IsValid);
        Assert.False(validator.Validate(new CreateTimelineNoteRequest(2026, 11, null, null, "  ")).IsValid);
        Assert.False(validator.Validate(new CreateTimelineNoteRequest(2026, 11, null, Guid.Empty, "x")).IsValid);
    }
}
