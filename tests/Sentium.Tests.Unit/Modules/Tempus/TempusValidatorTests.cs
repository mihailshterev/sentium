using FluentAssertions;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Dtos;
using Sentium.Tempus.Validation;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Tempus;

public sealed class TempusValidatorTests
{
    private static SaveTimelineTaskRequest Task(
        string title = "Book dentist",
        int year = 2026,
        int month = 11,
        DateOnly? date = null,
        IReadOnlyList<string>? tags = null) => new(title, null, year, month, date, null, TimelineTaskPriority.Normal, TimelineTaskStatus.Todo, tags);

    [Fact]
    public void SaveTask_AcceptsADayTask_AndAPinnedTask()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        validator.Validate(Task(date: new DateOnly(2026, 11, 14))).IsValid.Should().BeTrue();
        validator.Validate(Task()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SaveTask_RejectsADayOutsideTheMonth()
        => new SaveTimelineTaskRequestValidator().Validate(Task(date: new DateOnly(2026, 12, 1))).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("", 2026, 11)]
    [InlineData("Plan", 1999, 11)]
    [InlineData("Plan", 2026, 13)]
    public void SaveTask_RejectsMissingTitles_AndOutOfRangeMonths(string title, int year, int month)
        => new SaveTimelineTaskRequestValidator().Validate(Task(title, year, month)).IsValid.Should().BeFalse();

    [Fact]
    public void SaveTask_RejectsTagsWithCommas_AndTooManyTags()
    {
        var validator = new SaveTimelineTaskRequestValidator();

        validator.Validate(Task(tags: ["health, family"])).IsValid.Should().BeFalse();
        validator.Validate(Task(tags: [.. Enumerable.Range(0, 11).Select(i => $"t{i}")])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveTask_RejectsUnknownEnumValues()
    {
        var request = Task() with { Priority = (TimelineTaskPriority)42 };

        new SaveTimelineTaskRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Schedule_RequiresTheDayInTheTargetMonth()
    {
        var validator = new ScheduleTimelineTaskRequestValidator();

        validator.Validate(new ScheduleTimelineTaskRequest(2026, 11, new DateOnly(2026, 11, 30))).IsValid.Should().BeTrue();
        validator.Validate(new ScheduleTimelineTaskRequest(2026, 11, null)).IsValid.Should().BeTrue();
        validator.Validate(new ScheduleTimelineTaskRequest(2026, 11, new DateOnly(2026, 10, 31))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Note_RequiresContent()
    {
        var validator = new CreateTimelineNoteRequestValidator();

        validator.Validate(new CreateTimelineNoteRequest(2026, 11, null, null, "Looks good")).IsValid.Should().BeTrue();
        validator.Validate(new CreateTimelineNoteRequest(2026, 11, null, null, "  ")).IsValid.Should().BeFalse();
        validator.Validate(new CreateTimelineNoteRequest(2026, 11, null, Guid.Empty, "x")).IsValid.Should().BeFalse();
    }
}
