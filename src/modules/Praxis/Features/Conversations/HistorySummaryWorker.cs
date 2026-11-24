using Sentium.Infrastructure.Security;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Updates conversation summaries one at a time, each in a scope acting as the conversation's owner. A failure is
/// logged and skipped; the next turn asks again.
/// </summary>
internal sealed class HistorySummaryWorker(
    HistorySummaryQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<HistorySummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SummarizeAsync(request, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SummarizeAsync(HistorySummaryRequest request, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().ActAs(request.UserId);

            var passes = await scope.ServiceProvider.GetRequiredService<HistorySummarizer>()
                .SummarizeIfDueAsync(request.ConversationId, request.UserId, ct);

            if (passes > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Updated the summary of conversation {ConversationId} in {Passes} passes.", request.ConversationId, passes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not update the summary of conversation {ConversationId}.", request.ConversationId);
        }
    }
}
