using Sentium.Core.Results;

namespace Sentium.Probatio.Features.Evaluations;

internal interface IEvaluationService
{
    /// <summary>
    /// Freezes the suite version and the agent and queues the Evaluation. Invalid when the suite version has no
    /// cases, or the agent or model is unknown.
    /// </summary>
    Task<Result<EvaluationSummaryDto>> StartAsync(StartEvaluationRequest request, CancellationToken ct = default);

    Task<PagedResponse<EvaluationSummaryDto>> GetPagedAsync(Guid? suiteId, int page, int pageSize, CancellationToken ct = default);

    Task<EvaluationDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cancels a queued Evaluation at once, or tells a running one to stop (it ends Cancelled shortly). Conflict
    /// when it has already finished.
    /// </summary>
    Task<Result<EvaluationSummaryDto>> CancelAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a finished Evaluation with its results. Conflict while it is queued or running.
    /// </summary>
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
