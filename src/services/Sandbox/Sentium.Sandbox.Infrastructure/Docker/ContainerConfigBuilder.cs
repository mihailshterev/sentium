using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Docker;

/// <summary>
/// Builds a fully security-hardened <see cref="CreateContainerParameters"/> instance
/// for a sandbox worker container.
/// </summary>
internal sealed class ContainerConfigBuilder(IOptions<SandboxOptions> options)
{
    private readonly SandboxOptions _options = options.Value;

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
