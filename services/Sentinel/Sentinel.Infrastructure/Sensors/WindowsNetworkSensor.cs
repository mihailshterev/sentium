using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Sentinel.Application.Engine;
using Sentinel.Core.Events;
using Sentinel.Core.Sensors;
using Sentinel.Core.Sources;

namespace Sentinel.Infrastructure.Sensors;

public sealed class WindowsNetworkSensor(
    SentinelPolicyEngine engine,
    ILogger<WindowsNetworkSensor> logger) : INetworkSensor
{
    private readonly ConcurrentDictionary<string, byte> Cache = new();

    public async Task ScanAsync(Func<SentinelEvent, Task> onNewConnection, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onNewConnection);
        var connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();

        foreach (var conn in connections.Where(c => c.State == TcpState.Established))
        {
            if (IPAddress.IsLoopback(conn.RemoteEndPoint.Address))
            {
                continue;
            }

            var key = $"{conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port}";

            if (Cache.TryAdd(key, 0))
            {
                // P/Invoke 'GetExtendedTcpTable' here?
                var evt = CreateEnrichedEvent(conn);

                var decision = engine.Evaluate(evt);
                if (!decision.Allowed)
                {
                    await onNewConnection(evt);
                }
                //else
                //{
                //    logger.LogWarning("Policy Violation: {Remote} blocked.", conn.RemoteEndPoint);
                //}
            }
        }
    }

    private static SentinelEvent CreateEnrichedEvent(TcpConnectionInformation conn)
    {
        return new SentinelEvent(
            EventSources.Host,
            EventType.Network,
            TrafficDirection.Outbound,
            DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["remote_ip"] = conn.RemoteEndPoint.Address.ToString(),
                ["remote_port"] = conn.RemoteEndPoint.Port.ToString(),
                ["local_port"] = conn.LocalEndPoint.Port.ToString(),
                ["hostname"] = Environment.MachineName,
                ["user"] = Environment.UserName
            }
        );
    }
}
