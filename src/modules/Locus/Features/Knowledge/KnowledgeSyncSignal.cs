using Sentium.Infrastructure.BackgroundWork;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Wakes <see cref="KnowledgeSyncWorker"/> after a committed change queued work.
/// </summary>
internal sealed class KnowledgeSyncSignal : WakeSignal;
