namespace Sentium.Probatio.Entities;

/// <summary>
/// One text message of a repetition's conversation; <paramref name="Role"/> is <c>user</c> or <c>assistant</c>.
/// Tool calls are recorded separately as <see cref="RecordedToolCall"/>s.
/// </summary>
internal sealed record TranscriptMessage(string Role, string Text);
