using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Options;
using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Docker;

/// <summary>
/// Builds a fully security-hardened <see cref="CreateContainerParameters"/> instance
/// for a sandbox worker container or a hosted MCP server container.
/// </summary>
internal sealed class ContainerConfigBuilder(IOptions<SandboxOptions> options, IOptions<McpHostOptions> mcpOptions)
{
    /// <summary>
    /// Label marking MCP server containers (reaped on startup: the Sandbox keeps no state across restarts).
    /// </summary>
    public const string McpServerLabel = "sentium.mcp-server";

    public const string ServiceLabel = "sentium.service";

    public const string McpServiceValue = "mcp";

    private readonly SandboxOptions _options = options.Value;
    private readonly McpHostOptions _mcpOptions = mcpOptions.Value;

    /// <summary>
    /// A long-lived container running a stdio MCP server: the code-execution hardening (read-only root, no
    /// capabilities, no-new-privileges, pid/memory/cpu limits, tmpfs scratch space), an attachable stdin, and no
    /// network unless the spec names one the operator allows. Resource requests are clamped to the operator maxima.
    /// </summary>
    public CreateContainerParameters BuildMcp(string serverName, McpServerLaunchSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(spec);

        var memoryMb = Math.Clamp(spec.MemoryMb ?? _mcpOptions.DefaultMemoryMb, 32, _mcpOptions.MaxMemoryMb);
        var cpus = Math.Clamp(spec.CpuLimit ?? _mcpOptions.DefaultCpuLimit, 0.1, _mcpOptions.MaxCpuLimit);
        var memoryBytes = memoryMb * 1024L * 1024L;

        var env = new List<string> { "HOME=/tmp", "TMPDIR=/tmp", "TERM=dumb", "NO_UPDATE_NOTIFIER=1", "PYTHONUNBUFFERED=1" };
        env.AddRange((spec.Env ?? new Dictionary<string, string>()).Select(e => $"{e.Key}={e.Value}"));

        var hasNetwork = !string.IsNullOrWhiteSpace(spec.Network);

        return new CreateContainerParameters
        {
            Image = spec.Image,
            Cmd = spec.Args is { Count: > 0 } args ? [.. args] : null,
            Hostname = $"mcp-{serverName}",
            User = string.IsNullOrWhiteSpace(_mcpOptions.RunAsUser) ? null : _mcpOptions.RunAsUser,
            Env = env,

            OpenStdin = true,
            StdinOnce = false,
            AttachStdin = true,
            AttachStdout = true,
            AttachStderr = true,
            Tty = false,

            NetworkDisabled = !hasNetwork,

            Labels = new Dictionary<string, string>
            {
                { "sentium.managed", "true" },
                { ServiceLabel, McpServiceValue },
                { McpServerLabel, serverName }
            },

            HostConfig = new HostConfig
            {
                NetworkMode = hasNetwork ? spec.Network : "none",
                ReadonlyRootfs = true,
                Memory = memoryBytes,
                MemorySwap = memoryBytes,
                NanoCPUs = (long)(cpus * 1_000_000_000L),
                PidsLimit = _mcpOptions.PidsLimit,
                OomKillDisable = false,
                CapDrop = ["ALL"],
                CapAdd = [],
                Privileged = false,
                SecurityOpt = BuildSecurityOpts(),
                Ulimits = BuildUlimits(),
                Tmpfs = new Dictionary<string, string>
                {
                    { "/tmp", $"size={_mcpOptions.TmpfsSizeMb},noexec,nosuid,nodev,rw,mode=1777" }
                },
                AutoRemove = false,
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.No },
                LogConfig = new LogConfig
                {
                    Type = "json-file",
                    Config = new Dictionary<string, string>
                    {
                        { "max-size", "10m" },
                        { "max-file", "1" }
                    }
                }
            }
        };
    }

    public CreateContainerParameters Build(string image, string[] cmd, string hostJobDirectory, Guid jobId, ExecutionLanguage language)
    {
        var env = BuildEnv(language);
        var securityOpts = BuildSecurityOpts();
        var ulimits = BuildUlimits();

        return new CreateContainerParameters
        {
            Image = image,
            Cmd = cmd,
            WorkingDir = "/job",
            Hostname = $"sandbox-{jobId.ToString("N")[..12]}",

            NetworkDisabled = true,

            User = "nobody",

            Env = env,

            Labels = new Dictionary<string, string>
            {
                { "sentium.managed", "true" },
                { "sentium.job-id", jobId.ToString() },
                { "sentium.service", "sandbox" },
                { "sentium.language", language.ToString().ToLowerInvariant() }
            },

            HostConfig = new HostConfig
            {
                ReadonlyRootfs = _options.ReadonlyRootFs,
                Memory = _options.MemoryLimitBytes,
                MemorySwap = _options.MemoryLimitBytes,
                NanoCPUs = (long)(_options.CpuLimit * 1_000_000_000L),
                PidsLimit = _options.PidsLimit,
                OomKillDisable = false,
                ShmSize = 64 * 1024 * 1024,
                CapDrop = ["ALL"],
                CapAdd = [],
                Privileged = false,
                SecurityOpt = securityOpts,
                Ulimits = ulimits,
                Tmpfs = new Dictionary<string, string>
                {
                    { "/tmp", $"size={_options.TmpfsSizeMb},noexec,nosuid,nodev,rw,mode=1777" }
                },
                Mounts =
                [
                    new()
                    {
                        Type = "bind",
                        Source = hostJobDirectory,
                        Target = "/job",
                        ReadOnly = false
                    }
                ],
                AutoRemove = false,
                LogConfig = new LogConfig
                {
                    Type = "json-file",
                    Config = new Dictionary<string, string>
                    {
                        { "max-size", "10m" },
                        { "max-file", "1" }
                    }
                }
            }
        };
    }

    private static List<string> BuildEnv(ExecutionLanguage language)
    {
        var env = new List<string>
        {
            "HOME=/tmp",
            "TMPDIR=/tmp",
            "TERM=dumb"
        };

        if (language == ExecutionLanguage.Python)
        {
            env.Add("PYTHONDONTWRITEBYTECODE=1");
            env.Add("PYTHONUNBUFFERED=1");
            env.Add("PYTHONNOUSERSITE=1");
        }
        else if (language == ExecutionLanguage.Node)
        {
            env.Add("NODE_ENV=production");
            env.Add("NO_UPDATE_NOTIFIER=1");
        }

        return env;
    }

    private List<string> BuildSecurityOpts()
    {
        var opts = new List<string> { "no-new-privileges:true" };

        if (!string.IsNullOrWhiteSpace(_options.SeccompProfile))
        {
            opts.Add($"seccomp={_options.SeccompProfile}");
        }

        return opts;
    }

    private List<Ulimit> BuildUlimits()
    {
        return
        [
            new Ulimit
            {
                Name = "nofile",
                Soft = _options.NoFileLimitSoft,
                Hard = _options.NoFileLimitHard
            },
            new Ulimit
            {
                Name = "nproc",
                Soft = _options.PidsLimit,
                Hard = _options.PidsLimit * 2
            }
        ];
    }
}
