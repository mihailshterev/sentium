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

    /// <summary>
    /// Returns <see cref="CreateContainerParameters"/> configured with defence-in-depth security controls:
    /// <list type="bullet">
    ///   <item>Network disabled</item>
    ///   <item>Read-only root filesystem (writable /tmp via tmpfs, writable /job via bind mount)</item>
    ///   <item>All Linux capabilities dropped</item>
    ///   <item>No new privileges (seccomp + AppArmor default profiles)</item>
    ///   <item>CPU, memory and PID hard limits</item>
    ///   <item>Non-root user (nobody)</item>
    ///   <item>OOM-kill enabled</item>
    /// </list>
    /// </summary>
    public CreateContainerParameters Build(
        string image,
        string[] cmd,
        string hostJobDirectory,
        Guid jobId,
        ExecutionLanguage language)
    {
        var env = BuildEnv(language);
        var securityOpts = BuildSecurityOpts();
        var ulimits = BuildUlimits();

        return new CreateContainerParameters
        {
            // ── Identity ────────────────────────────────────────────────────
            Image = image,
            Cmd = cmd,
            WorkingDir = "/job",
            Hostname = $"sandbox-{jobId.ToString("N")[..12]}",

            // ── Network isolation ───────────────────────────────────────────
            // Prevents the container from making any outbound connections.
            NetworkDisabled = true,

            // ── Process isolation ───────────────────────────────────────────
            // Run as the unprivileged `nobody` user; prevents writing to
            // system directories even if ReadonlyRootfs were disabled.
            User = "nobody",

            // ── Environment ─────────────────────────────────────────────────
            Env = env,

            // ── Observability labels ────────────────────────────────────────
            Labels = new Dictionary<string, string>
            {
                { "sentium.managed", "true" },
                { "sentium.job-id", jobId.ToString() },
                { "sentium.service", "sandbox" },
                { "sentium.language", language.ToString().ToLowerInvariant() }
            },

            HostConfig = new HostConfig
            {
                // ── Root filesystem ─────────────────────────────────────────
                // The rootfs is immutable; only /tmp (tmpfs) and /job (bind) are writable.
                ReadonlyRootfs = _options.ReadonlyRootFs,

                // ── Resource hard limits ────────────────────────────────────
                Memory = _options.MemoryLimitBytes,
                // Disable swap entirely: MemorySwap == Memory means no swap.
                MemorySwap = _options.MemoryLimitBytes,
                // Fraction-of-a-core expressed in nano-CPUs (1 CPU = 1,000,000,000 nano-CPUs).
                NanoCPUs = (long)(_options.CpuLimit * 1_000_000_000L),
                // Hard cap on processes + threads. Mitigates fork-bomb attacks.
                PidsLimit = _options.PidsLimit,
                // Allow the OOM-killer to terminate this container under memory pressure.
                OomKillDisable = false,
                // Shared memory; 64 MB is generous for scripts.
                ShmSize = 64 * 1024 * 1024,

                // ── Privilege reduction ─────────────────────────────────────
                // Drop ALL Linux capabilities. Add none back — sandbox code
                // should never need any kernel-level privilege.
                CapDrop = ["ALL"],
                CapAdd = [],
                Privileged = false,

                // ── Security options ────────────────────────────────────────
                SecurityOpt = securityOpts,

                // ── File descriptor limits ──────────────────────────────────
                Ulimits = ulimits,

                // ── Writable scratch space ──────────────────────────────────
                // noexec: code written to /tmp cannot be executed directly
                // nosuid: setuid bits are ignored
                // nodev:  device files are ignored
                Tmpfs = new Dictionary<string, string>
                {
                    { "/tmp", $"size={_options.TmpfsSizeMb},noexec,nosuid,nodev,rw,mode=1777" }
                },

                // ── Job directory bind-mount ────────────────────────────────
                // The host job directory is mounted at /job.
                // Code can read its input files and write output files here.
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

                // ── Container lifecycle ─────────────────────────────────────
                // We remove the container ourselves to capture its exit code first.
                AutoRemove = false,

                // ── Log driver ──────────────────────────────────────────────
                // Bounded local log to prevent log flooding on the host.
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
            // Redirect home/cache writes to the tmpfs mount so they don't
            // fail against the read-only rootfs.
            "HOME=/tmp",
            "TMPDIR=/tmp",
            // Prevent tty-detection from blocking output.
            "TERM=dumb"
        };

        if (language == ExecutionLanguage.Python)
        {
            // Disable .pyc bytecode generation (would fail on read-only fs).
            env.Add("PYTHONDONTWRITEBYTECODE=1");
            // Force stdout/stderr to be unbuffered so we capture partial output.
            env.Add("PYTHONUNBUFFERED=1");
            // Disable user-site packages and other Python path expansions.
            env.Add("PYTHONNOUSERSITE=1");
        }
        else if (language == ExecutionLanguage.Node)
        {
            env.Add("NODE_ENV=production");
            // Disable npm update checks that generate network traffic.
            env.Add("NO_UPDATE_NOTIFIER=1");
        }

        return env;
    }

    private List<string> BuildSecurityOpts()
    {
        // no-new-privileges: prevents the process from gaining privileges via
        // setuid binaries or file capabilities after exec.
        var opts = new List<string> { "no-new-privileges:true" };

        if (!string.IsNullOrWhiteSpace(_options.SeccompProfile))
        {
            opts.Add($"seccomp={_options.SeccompProfile}");
        }
        // When SeccompProfile is empty the Docker daemon applies its built-in
        // default seccomp profile, which is the correct secure default.

        return opts;
    }

    private List<Ulimit> BuildUlimits()
    {
        return
        [
            // Limit open file descriptors.
            new Ulimit
            {
                Name = "nofile",
                Soft = _options.NoFileLimitSoft,
                Hard = _options.NoFileLimitHard
            },
            // Belt-and-suspenders process limit alongside PidsLimit above.
            new Ulimit
            {
                Name = "nproc",
                Soft = _options.PidsLimit,
                Hard = _options.PidsLimit * 2
            }
        ];
    }
}
