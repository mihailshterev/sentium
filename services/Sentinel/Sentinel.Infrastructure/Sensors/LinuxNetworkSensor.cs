using System.Globalization;
using System.Net;
using System.Collections.Concurrent;
using Sentinel.Core.Events;
using Sentinel.Application.Engine;
using Microsoft.Extensions.Logging;
using Sentinel.Core.Sensors;
using Sentinel.Core.Sources;

namespace Sentinel.Infrastructure.Sensors;

public sealed class LinuxNetworkSensor(
    SentinelPolicyEngine engine,
    ILogger<LinuxNetworkSensor> logger) : INetworkSensor
{
    private readonly ConcurrentDictionary<string, byte> Cache = new();

    private static readonly string[] TcpPaths = ["/proc/net/tcp", "/proc/net/tcp6"];

    public async Task ScanAsync(Func<SentinelEvent, Task> onDetected, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onDetected);
        var connections = GetActiveConnections();

        foreach (var conn in connections)
        {
            var key = $"{conn.RemoteIp}:{conn.RemotePort}";

            if (Cache.TryAdd(key, 0))
            {
                var evt = CreateEvent(conn);
                var decision = engine.Evaluate(evt);

                if (decision.Allowed)
                {
                    await onDetected(evt);
                }
                else
                {
                    logger.LogWarning("[NET DENY] {Process} ({PID}) -> {Remote}",
                        conn.ProcessName, conn.Pid, conn.RemoteIp);
                }
            }
        }
    }

    private List<LinuxConnection> GetActiveConnections()
    {
        var results = new List<LinuxConnection>();
        foreach (var path in TcpPaths)
        {
            if (!File.Exists(path)) continue;

            var lines = File.ReadLines(path).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 10 || parts[3] != "01") continue; // 01 = ESTABLISHED

                var remoteParts = parts[2].Split(':');
                var ip = ParseHexIp(remoteParts[0]);

                if (IPAddress.IsLoopback(ip)) continue;

                var port = int.Parse(remoteParts[1], NumberStyles.HexNumber);
                var inode = parts[9];

                var pid = FindPidByInode(inode);
                var procName = pid != -1 ? GetProcessName(pid) : "unknown";

                results.Add(new LinuxConnection(ip.ToString(), port, pid, procName));
            }
        }
        return results;
    }

    private static IPAddress ParseHexIp(string hex)
    {
        byte[] bytes = Enumerable.Range(0, hex.Length / 2)
            .Select(i => byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber))
            .Reverse().ToArray();
        return new IPAddress(bytes);
    }

    private static int FindPidByInode(string inode)
    {
        var socketMarker = $"socket:[{inode}]";

        foreach (var procDir in Directory.EnumerateDirectories("/proc"))
        {
            var dirName = Path.GetFileName(procDir);
            if (!int.TryParse(dirName, out var pid)) continue;

            var fdPath = Path.Combine(procDir, "fd");
            if (!Directory.Exists(fdPath)) continue;

            try
            {
                var descriptors = Directory.EnumerateFiles(fdPath);
                foreach (var fd in descriptors)
                {
                    var linkInfo = File.ResolveLinkTarget(fd, returnFinalTarget: true);

                    if (linkInfo != null && linkInfo.FullName.Equals(socketMarker, StringComparison.Ordinal))
                    {
                        return pid;
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return -1;
    }

    private string GetProcessName(int pid) =>
        File.Exists($"/proc/{pid}/comm") ? File.ReadAllText($"/proc/{pid}/comm").Trim() : "unknown";

    private SentinelEvent CreateEvent(LinuxConnection conn) =>
        new(EventSources.Host, EventType.Network, TrafficDirection.Outbound, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["remote"] = conn.RemoteIp,
                ["port"] = conn.RemotePort.ToString(),
                ["pid"] = conn.Pid.ToString(),
                ["process"] = conn.ProcessName
            });
}

public record LinuxConnection(string RemoteIp, int RemotePort, int Pid, string ProcessName);
