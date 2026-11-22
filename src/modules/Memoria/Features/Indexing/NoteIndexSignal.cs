using Sentium.Infrastructure.BackgroundWork;

namespace Sentium.Memoria.Features.Indexing;

/// <summary>
/// Wakes <see cref="NoteIndexWorker"/> after a committed sync queued work.
/// </summary>
internal sealed class NoteIndexSignal : WakeSignal;
