using Sentium.Infrastructure.BackgroundWork;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Drains the knowledge-sync outbox in the background so asset saves never wait for embeddings.
/// </summary>
internal sealed class KnowledgeSyncWorker(
    IServiceScopeFactory scopeFactory,
    KnowledgeSyncSignal signal,
    TimeProvider timeProvider,
    ILogger<KnowledgeSyncWorker> logger)
    : OutboxDrainWorker<KnowledgeSyncProcessor>(scopeFactory, signal, timeProvider, logger, "Knowledge sync");
