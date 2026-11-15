namespace Sentium.Praxis.Features.Speech;

internal sealed record SpeechAudio(ReadOnlyMemory<byte> Data, string ContentType);
