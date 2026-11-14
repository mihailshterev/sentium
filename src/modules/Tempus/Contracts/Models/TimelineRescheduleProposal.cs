namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// A new place on the timeline an assistant suggests for an existing task. <paramref name="Date"/>
/// <see langword="null"/> pins it to the month.
/// </summary>
public sealed record TimelineRescheduleProposal(Guid TaskId, int Year, int Month, DateOnly? Date, string? Rationale);
