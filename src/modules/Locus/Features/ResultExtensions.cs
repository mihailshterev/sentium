using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sentium.Core.Results;

namespace Sentium.Locus.Features;

internal static class ResultExtensions
{
    /// <summary>
    /// Re-types a non-success result (its status and error carry over).
    /// </summary>
    public static Result<TOut> Propagate<TIn, TOut>(this Result<TIn> result) => result.Status switch
    {
        ResultStatus.NotFound => Result<TOut>.NotFound(result.Error),
        ResultStatus.Conflict => Result<TOut>.Conflict(result.Error ?? "Conflict."),
        ResultStatus.Invalid => Result<TOut>.Invalid(result.Error ?? "Invalid request."),
        _ => throw new InvalidOperationException("Only non-success results can be propagated.")
    };

    public static bool IsUniqueViolation(this DbUpdateException exception, string indexName)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
           && string.Equals(pg.ConstraintName, indexName, StringComparison.Ordinal);
}
