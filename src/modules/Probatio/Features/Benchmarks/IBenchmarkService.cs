using Sentium.Core.Results;

namespace Sentium.Probatio.Features.Benchmarks;

internal interface IBenchmarkService
{
    /// <summary>
    /// Queues one Evaluation per model, all of the same suite version and agent and graded by the same Judge.
    /// Invalid when any model, the agent, the suite version or the Judge is unusable; then nothing is queued.
    /// </summary>
    Task<Result<BenchmarkSummaryDto>> StartAsync(StartBenchmarkRequest request, CancellationToken ct = default);

    Task<PagedResponse<BenchmarkSummaryDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    Task<BenchmarkDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cancels every Evaluation of the Benchmark that is still queued or running.
    /// </summary>
    Task<Result<BenchmarkSummaryDto>> CancelAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes the Benchmark with its Evaluations. Conflict while any of them is queued or running.
    /// </summary>
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
