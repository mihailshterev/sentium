namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// A task an assistant suggests adding. <paramref name="Date"/> <see langword="null"/> pins it to the month.
/// </summary>
public sealed record TimelineTaskProposal(
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    string? Rationale);
