namespace Sentium.Praxis.Features.Speech;

/// <param name="Voice">A Voice id from the status response; <see langword="null"/> uses the default Voice.</param>
/// <param name="Speed">Speaking rate multiplier; <see langword="null"/> is normal speed.</param>
internal sealed record SpeakRequest(string Text, string? Voice = null, float? Speed = null);
