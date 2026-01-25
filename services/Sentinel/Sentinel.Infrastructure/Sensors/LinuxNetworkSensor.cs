using Microsoft.Extensions.Logging;
using Sentinel.Application.Engine;
using Sentinel.Core.Events;
using Sentinel.Core.Sensors;
using Sentinel.Core.Sources;

namespace Sentinel.Infrastructure.Sensors;

public sealed class LinuxNetworkSensor : INetworkSensor
{
    private readonly SentinelPolicyEngine Engine;
    private readonly ILogger<LinuxNetworkSensor> Logger;

    public LinuxNetworkSensor(SentinelPolicyEngine engine, ILogger<LinuxNetworkSensor> logger)
    {
        Engine = engine;
        Logger = logger;
    }

    public void Scan()
    {
        if (!File.Exists("/proc/net/tcp"))
        {
            return;
        }

        var lines = File.ReadAllLines("/proc/net/tcp").Skip(1);

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            var remote = parts[2];
            if (remote.StartsWith("0100007F"))
            {
                continue;
            }

            EmitEvent(remote);
        }
    }

    private void EmitEvent(string remote)
    {
        var evt = new SentinelEvent(
            EventSources.Host,
            EventType.Network,
            TrafficDirection.Outbound,
            DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["remote"] = remote
            });

        var decision = Engine.Evaluate(evt);

        Logger.LogInformation("{Message}",
            decision.Allowed
                ? $"[NET ALLOW] {remote}"
                : $"[NET DENY] {remote} → {decision.Reason}");
    }
}
