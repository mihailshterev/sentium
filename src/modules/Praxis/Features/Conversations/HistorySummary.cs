namespace Sentium.Praxis.Features.Conversations;

/// <param name="Through">The timestamp of the newest message the summary covers.</param>
internal sealed record HistorySummary(string Text, DateTime Through);
