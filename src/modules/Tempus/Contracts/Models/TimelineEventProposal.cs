namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// A Calendar Event an assistant suggests adding. <paramref name="Date"/> <see langword="null"/> pins it to the
/// month; <paramref name="StartTime"/> and <paramref name="EndDate"/> need a <paramref name="Date"/>.
/// </summary>
public sealed record TimelineEventProposal(
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    IReadOnlyList<string> Tags,
    string? Rationale);
