using System.Diagnostics;
using System.Runtime.InteropServices;
using Watchdog.Core.Metrics;

namespace Watchdog.Application;

public sealed class WatchdogService : IWatchdog
{
    public SystemMetrics GetMetrics()
    {
        var process = Process.GetCurrentProcess();

        return new SystemMetrics
        {
            Host = new HostInfo
            {
                MachineName = Environment.MachineName,
                OsDescription = RuntimeInformation.OSDescription,
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            },
            Memory = GetMemoryInfo(),
            Cpu = GetCpuInfo(),
            Disks = GetDiskInfo(),
            Process = new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                WorkingSetMb = process.WorkingSet64 / (1024.0 * 1024.0),
                PrivateMemoryMb = process.PrivateMemorySize64 / (1024.0 * 1024.0),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                StartTime = process.StartTime.ToUniversalTime(),
                CpuTime = process.TotalProcessorTime
            }
        };
    }

    private static MemoryInfo GetMemoryInfo()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var totalAvailable = gcInfo.TotalAvailableMemoryBytes;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new MemoryInfo
            {
                TotalMb = totalAvailable / (1024.0 * 1024.0),
                UsedMb = (totalAvailable - gcInfo.HighMemoryLoadThresholdBytes) / (1024.0 * 1024.0),
                AvailableMb = gcInfo.HighMemoryLoadThresholdBytes / (1024.0 * 1024.0),
                MemoryLoadPercent = gcInfo.MemoryLoadBytes * 100.0 / totalAvailable,
                GcHeapSizeMb = gcInfo.HeapSizeBytes / (1024.0 * 1024.0),
                GcGen0Collections = GC.CollectionCount(0),
                GcGen1Collections = GC.CollectionCount(1),
                GcGen2Collections = GC.CollectionCount(2)
            };
        }

        // Linux: parse /proc/meminfo
        try
        {
            var memInfo = File.ReadAllLines("/proc/meminfo");
            long totalKb = 0, availableKb = 0;
            foreach (var line in memInfo)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    totalKb = ParseMemInfoValue(line);
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    availableKb = ParseMemInfoValue(line);
                }
            }

            return new MemoryInfo
            {
                TotalMb = totalKb / 1024.0,
                AvailableMb = availableKb / 1024.0,
                UsedMb = (totalKb - availableKb) / 1024.0,
                MemoryLoadPercent = totalKb > 0 ? (totalKb - availableKb) * 100.0 / totalKb : 0,
                GcHeapSizeMb = gcInfo.HeapSizeBytes / (1024.0 * 1024.0),
                GcGen0Collections = GC.CollectionCount(0),
                GcGen1Collections = GC.CollectionCount(1),
                GcGen2Collections = GC.CollectionCount(2)
            };
        }
        catch
        {
            return new MemoryInfo
            {
                TotalMb = totalAvailable / (1024.0 * 1024.0),
                MemoryLoadPercent = gcInfo.MemoryLoadBytes * 100.0 / totalAvailable,
                GcHeapSizeMb = gcInfo.HeapSizeBytes / (1024.0 * 1024.0),
                GcGen0Collections = GC.CollectionCount(0),
                GcGen1Collections = GC.CollectionCount(1),
                GcGen2Collections = GC.CollectionCount(2)
            };
        }
    }

    private static CpuInfo GetCpuInfo()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
        var cpuUsage = uptime.TotalMilliseconds > 0
            ? process.TotalProcessorTime.TotalMilliseconds / (uptime.TotalMilliseconds * Environment.ProcessorCount) * 100.0
            : 0;

        return new CpuInfo
        {
            ProcessorCount = Environment.ProcessorCount,
            ProcessCpuPercent = Math.Round(cpuUsage, 2),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private static List<DiskInfo> GetDiskInfo()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new DiskInfo
            {
                Name = d.Name,
                Label = d.VolumeLabel,
                FileSystem = d.DriveFormat,
                TotalGb = d.TotalSize / (1024.0 * 1024.0 * 1024.0),
                AvailableGb = d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0),
                UsedGb = (d.TotalSize - d.AvailableFreeSpace) / (1024.0 * 1024.0 * 1024.0),
                UsagePercent = d.TotalSize > 0
                    ? (d.TotalSize - d.AvailableFreeSpace) * 100.0 / d.TotalSize
                    : 0
            })
            .ToList();
    }

    private static long ParseMemInfoValue(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return 0;
        }
        var valStr = parts[1].Replace("kB", "").Trim();
        return long.TryParse(valStr, out var val) ? val : 0;
    }
}
