using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Sentium.Infrastructure.Extensions;

/// <summary>
/// Runs an operation in one transaction. Connection failures are not retried (the database is local); the one retry
/// kept is for a serialization failure, which a <see cref="IsolationLevel.Serializable"/> transaction must expect: the
/// operation is run again from the start and then sees the transaction it lost to.
/// </summary>
public static class DatabaseTransactionExtensions
{
    private const int MaxSerializationAttempts = 3;

    public static Task ExecuteInTransactionAsync(
        this DatabaseFacade database,
        Func<Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return database.ExecuteInTransactionAsync(
            async () =>
            {
                await operation();
                return true;
            },
            isolationLevel,
            ct);
    }

    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DatabaseFacade database,
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await database.BeginTransactionAsync(isolationLevel, ct);
                var result = await operation();
                await tx.CommitAsync(ct);
                return result;
            }
            catch (Exception ex) when (attempt < MaxSerializationAttempts && IsSerializationFailure(ex))
            {
            }
        }
    }

    private static bool IsSerializationFailure(Exception ex)
    {
        return (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.SerializationFailure;
    }
}
