using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;

namespace Sentium.Infrastructure.Extensions;

public static class DatabaseTransactionExtensions
{
    public static Task ExecuteInTransactionAsync(
        this DatabaseFacade database,
        Func<Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await database.BeginTransactionAsync(isolationLevel, ct);
            await operation();
            await tx.CommitAsync(ct);
        });
    }

    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DatabaseFacade database,
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        T result = default!;
        await database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await database.BeginTransactionAsync(isolationLevel, ct);
            result = await operation();
            await tx.CommitAsync(ct);
        });

        return result;
    }
}
