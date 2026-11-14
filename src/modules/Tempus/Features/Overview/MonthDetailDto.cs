using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Overview;

/// <param name="Proposals">Pending proposals that target this month or move a task out of it.</param>
internal sealed record MonthDetailDto(
    int Year,
    int Month,
    IReadOnlyList<TimelineTaskDto> Tasks,
    IReadOnlyList<TimelineNoteDto> Notes,
    IReadOnlyList<TimelineProposalDto> Proposals);
