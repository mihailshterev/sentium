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
        return database.CreateExecutionStrategy().ExecuteAsync(
            () => RunInTransactionAsync(database, operation, isolationLevel, ct));
    }

    public static Task<T> ExecuteInTransactionAsync<T>(
        this DatabaseFacade database,
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.CreateExecutionStrategy().ExecuteAsync(
            () => RunInTransactionAsync(database, operation, isolationLevel, ct));
    }

    private static async Task RunInTransactionAsync(
        DatabaseFacade database,
        Func<Task> operation,
        IsolationLevel isolationLevel,
        CancellationToken ct)
    {
        await using var tx = await database.BeginTransactionAsync(isolationLevel, ct);
        await operation();
        await tx.CommitAsync(ct);
    }

    private static async Task<T> RunInTransactionAsync<T>(
        DatabaseFacade database,
        Func<Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken ct)
    {
        await using var tx = await database.BeginTransactionAsync(isolationLevel, ct);
        var result = await operation();
        await tx.CommitAsync(ct);
        return result;
    }
}
