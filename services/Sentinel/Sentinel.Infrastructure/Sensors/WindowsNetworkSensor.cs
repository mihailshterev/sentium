using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Sentinel.Application.Engine;
using Sentinel.Core.Events;
using Sentinel.Core.Sensors;
using Sentinel.Core.Sources;

namespace Sentinel.Infrastructure.Sensors;

public sealed class WindowsNetworkSensor : INetworkSensor
{
    private readonly SentinelPolicyEngine Engine;
    private readonly ILogger<WindowsNetworkSensor> Logger;

    public WindowsNetworkSensor(
        SentinelPolicyEngine engine,
        ILogger<WindowsNetworkSensor> logger)
    {
        Engine = engine;
        Logger = logger;
    }

    public void Scan()
    {
        var connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();

        foreach (var conn in connections)
        {
            if (conn.State != TcpState.Established)
            {
                continue;
            }

            if (IPAddress.IsLoopback(conn.RemoteEndPoint.Address))
            {
                continue;
            }

            var evt = new SentinelEvent(
                EventSources.Host,
                "Network",
                "Outbound",
                DateTime.UtcNow,
                new Dictionary<string, string>
                {
                    ["remoteIp"] = conn.RemoteEndPoint.Address.ToString(),
                    ["remotePort"] = conn.RemoteEndPoint.Port.ToString()
                }
            );

            var decision = Engine.Evaluate(evt);

            if (decision.Allowed)
            {
                Logger.LogInformation("Outbound connection allowed: {Remote}", conn.RemoteEndPoint);
            }
            else
            {
                Logger.LogWarning("Outbound connection BLOCKED: {Remote} Reason={Reason}", conn.RemoteEndPoint, decision.Reason);
            }
        }
    }
}
