using Microsoft.EntityFrameworkCore;
using Sentium.Codex.Data;
using Sentium.Identity.Contracts;

namespace Sentium.Codex.Features.Rag;

/// <summary>
/// Deletes a removed user's knowledge chunks in every collection: their files, memories and private learnings.
/// Shared chunks carry no user and stay.
/// </summary>
internal sealed class CodexUserDataRemover(IServiceScopeFactory scopeFactory, ILogger<CodexUserDataRemover> logger) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodexDbContext>();

        var deleted = await db.KnowledgeChunks.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} knowledge chunks of removed user {UserId}.", deleted, userId);
        }
    }
}
