using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Security;

namespace Sentium.Memoria.Features.Notes;

/// <summary>
/// Deletes a removed user's vault; the index worker then removes its knowledge-base chunks.
/// </summary>
internal sealed class MemoriaUserDataRemover(IServiceScopeFactory scopeFactory, ILogger<MemoriaUserDataRemover> logger) : IUserRemovalObserver
{
    public async Task OnUserRemovedAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        if (await scope.ServiceProvider.GetRequiredService<VaultEraser>().EraseAsync(userId, ct) && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted the vault of removed user {UserId}.", userId);
        }
    }
}
