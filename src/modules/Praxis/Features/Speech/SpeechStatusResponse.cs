namespace Sentium.Praxis.Features.Speech;

/// <summary>
/// What the portal needs to decide whether to show voice controls. A half that is switched off or whose server
/// is down reports <see langword="false"/>; that is the normal state of a deployment without speech, not an error.
/// </summary>
/// <param name="Voices">Empty when synthesis is unavailable.</param>
internal sealed record SpeechStatusResponse(
    bool Transcription,
    bool Synthesis,
    string DefaultVoice,
    IReadOnlyList<SpeechVoice> Voices,
    int MaxTextLength);
