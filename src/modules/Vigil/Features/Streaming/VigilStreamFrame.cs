namespace Sentium.Vigil.Features.Streaming;

public sealed record VigilStreamFrame<T>(string Type, T Data);
