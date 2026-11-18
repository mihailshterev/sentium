using Microsoft.EntityFrameworkCore;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Security;
using Sentium.Probatio.Data;

namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// Deletes a removed user's Eval Suites with all their versions, and their Benchmarks and Evaluations with all results.
/// </summary>
internal sealed class ProbatioUserDataRemover(IServiceScopeFactory scopeFactory, ILogger<ProbatioUserDataRemover> logger) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<ProbatioDbContext>();

        var deleted =
            await db.Benchmarks.IgnoreQueryFilters().Where(b => b.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.Evaluations.IgnoreQueryFilters().Where(e => e.UserId == userId).ExecuteDeleteAsync(ct)
            + await db.EvalSuites.IgnoreQueryFilters().Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} Eval Suites, Benchmarks and Evaluations of removed user {UserId}.", deleted, userId);
        }
    }
}
