namespace Sentium.Vigil.Dtos;

public sealed record VigilStreamFrame<T>(string Type, T Data);

public sealed record VigilConnectedFrame(string Type = "connected");
