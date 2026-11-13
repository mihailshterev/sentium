namespace Sentium.Vigil.Features.Streaming;

public sealed record VigilStreamFrame<T>(string Type, T Data);

public sealed record VigilConnectedFrame(string Type = "connected");
