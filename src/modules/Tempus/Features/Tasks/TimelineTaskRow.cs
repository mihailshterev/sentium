using Sentium.Tempus.Entities;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// An Entry as read for display: with its note count and, for an occurrence, its Series.
/// </summary>
internal sealed record TimelineTaskRow(TimelineTask Task, int NoteCount, TimelineSeries? Series)
{
    public static IQueryable<TimelineTaskRow> Select(IQueryable<TimelineTask> tasks)
        => tasks.Select(t => new TimelineTaskRow(t, t.Notes.Count, t.Series));
}
