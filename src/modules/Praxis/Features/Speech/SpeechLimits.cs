namespace Sentium.Praxis.Features.Speech;

internal static class SpeechLimits
{
    /// <summary>
    /// Largest recording accepted for transcription. Push-to-talk clips are a few hundred kilobytes; this is a
    /// ceiling, not a target.
    /// </summary>
    public const long MaxAudioBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Longest text accepted for one synthesis request. The portal speaks sentence by sentence.
    /// </summary>
    public const int MaxTextLength = 1000;

    public const int MaxVoiceIdLength = 64;
    public const float MinSpeed = 0.5f;
    public const float MaxSpeed = 2.0f;
}
