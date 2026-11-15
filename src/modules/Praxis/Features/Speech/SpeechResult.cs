namespace Sentium.Praxis.Features.Speech;

/// <summary>
/// <paramref name="Value"/> is set only when <paramref name="Outcome"/> is <see cref="SpeechOutcome.Success"/>.
/// </summary>
internal readonly record struct SpeechResult<T>(SpeechOutcome Outcome, T? Value)
    where T : class
{
    public static SpeechResult<T> Success(T value) => new(SpeechOutcome.Success, value);

    public static SpeechResult<T> Unavailable { get; } = new(SpeechOutcome.Unavailable, null);

    public static SpeechResult<T> Failed { get; } = new(SpeechOutcome.Failed, null);
}
