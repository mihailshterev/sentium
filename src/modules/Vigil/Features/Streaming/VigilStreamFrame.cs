namespace Sentium.Vigil.Features.Streaming;

internal sealed record VigilStreamFrame<T>(string Type, T Data);
