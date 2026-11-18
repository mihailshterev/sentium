using Sentium.Core.Results;

namespace Sentium.Probatio.Features.Suites;

internal interface IEvalSuiteService
{
    Task<PagedResponse<EvalSuiteSummaryDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// The suite with its current cases; <see langword="null"/> when it does not exist or is someone else's.
    /// </summary>
    Task<EvalSuiteDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The suite with the cases of an earlier (or the current) version.
    /// </summary>
    Task<EvalSuiteDto?> GetVersionAsync(Guid id, int version, CancellationToken ct = default);

    /// <summary>
    /// Every version of the suite, newest first; <see langword="null"/> when the suite does not exist.
    /// </summary>
    Task<IReadOnlyList<EvalSuiteVersionDto>?> GetVersionsAsync(Guid id, CancellationToken ct = default);

    Task<EvalSuiteDto> CreateAsync(CreateEvalSuiteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Saves an edit. Conflict when the suite moved past <see cref="UpdateEvalSuiteRequest.BaseVersion"/>.
    /// </summary>
    Task<Result<EvalSuiteDto>> UpdateAsync(Guid id, UpdateEvalSuiteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the suite and all its versions.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
