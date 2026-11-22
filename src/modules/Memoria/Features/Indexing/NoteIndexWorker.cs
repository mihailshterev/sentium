using Sentium.Infrastructure.BackgroundWork;

namespace Sentium.Memoria.Features.Indexing;

/// <summary>
/// Drains the note index outbox in the background so syncs never wait for embeddings.
/// </summary>
internal sealed class NoteIndexWorker(
    IServiceScopeFactory scopeFactory,
    NoteIndexSignal signal,
    TimeProvider timeProvider,
    ILogger<NoteIndexWorker> logger)
    : OutboxDrainWorker<NoteIndexProcessor>(scopeFactory, signal, timeProvider, logger, "Vault note index");
