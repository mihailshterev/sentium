namespace Sentium.Watchdog.Core.Dtos;

public sealed record WatchdogStreamFrame<T>(string Type, T Data);

public sealed record WatchdogConnectedFrame(string Type = "connected");
