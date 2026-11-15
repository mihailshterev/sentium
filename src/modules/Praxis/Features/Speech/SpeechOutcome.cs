namespace Sentium.Praxis.Features.Speech;

internal enum SpeechOutcome
{
    Success,

    /// <summary>
    /// The speech server is not configured or cannot be reached.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The speech server answered but could not process the input (for example an unreadable recording).
    /// </summary>
    Failed
}
