namespace Sentium.Praxis.Features.Speech;

/// <summary>
/// Speech recognition and synthesis on the home server's speech containers. Audio only passes through: nothing
/// here stores or logs a recording, a transcript or synthesised audio.
/// </summary>
internal interface ISpeechService
{
    /// <summary>
    /// Reports which halves of speech are usable right now and the Voices on offer. Never fails.
    /// </summary>
    Task<SpeechStatusResponse> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Transcribes one recording. The transcript may be empty when nothing was said.
    /// </summary>
    Task<SpeechResult<string>> TranscribeAsync(Stream audio, CancellationToken ct = default);

    /// <param name="voice"><see langword="null"/> uses the configured default Voice.</param>
    Task<SpeechResult<SpeechAudio>> SynthesizeAsync(string text, string? voice, float? speed, CancellationToken ct = default);
}
