using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Docker;
using Sentium.Sandbox.Options;

namespace Sentium.Sandbox.Mcp;

/// <summary>
/// Runs stdio MCP servers in hardened, long-lived containers and holds one MCP client session per server (Docker attach
/// ⇄ <see cref="ContainerStdioBridge"/> ⇄ <see cref="StreamClientTransport"/> ⇄ <see cref="McpClient"/>). Launch specs
/// live in memory only - the Sandbox stays stateless; Sentium.Web re-sends the spec when the Sandbox does not know a
/// server (e.g. after a restart). <see cref="EnsureAsync"/> is idempotent: the same spec returns the running server, a
/// changed spec replaces its container.
/// </summary>
internal sealed partial class McpContainerManager(
    IDockerClient docker,
    DockerStdioAttach attacher,
    DockerImageService images,
    ContainerConfigBuilder configs,
    IOptions<McpHostOptions> options,
    TimeProvider time,
    ILoggerFactory loggerFactory,
    ILogger<McpContainerManager> logger) : IMcpServerHost, IAsyncDisposable
{
    private static readonly Implementation ClientInfo = new() { Name = "Sentium.Sandbox", Version = "1.0.0" };

    private readonly McpHostOptions settings = options.Value;
    private readonly ConcurrentDictionary<string, HostedServer> servers = new(StringComparer.Ordinal);

    public bool Enabled => settings.Enabled;

    public async Task<McpServerStatus> EnsureAsync(string name, McpServerLaunchSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        CheckAllowed(name, spec);

        var hash = HashSpec(spec);
        var server = servers.GetOrAdd(name, n => new HostedServer(n));

        await server.Gate.WaitAsync(ct);
        try
        {
            server.Spec = spec;

            if (server.IsRunning && server.SpecHash == hash)
            {
                return server.ToStatus();
            }

            await StopContainerAsync(server);
            await StartAsync(server, spec, hash, ct);
            return server.ToStatus();
        }
        finally
        {
            server.Gate.Release();
        }
    }

    public async Task<ListToolsResult?> ListToolsAsync(string name, CancellationToken ct)
    {
        var client = await GetClientAsync(name, ct);
        if (client is null)
        {
            return null;
        }

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return new ListToolsResult { Tools = [.. tools.Select(t => t.ProtocolTool)] };
    }

    public async Task<CallToolResult?> CallToolAsync(string name, CallToolRequestParams request, CancellationToken ct)
    {
        var client = await GetClientAsync(name, ct);
        return client is null ? null : await client.CallToolAsync(request, ct);
    }

    public McpServerStatus? GetStatus(string name) => servers.TryGetValue(name, out var server) ? server.ToStatus() : null;

    public async Task<bool> StopAsync(string name)
    {
        if (!servers.TryRemove(name, out var server))
        {
            return false;
        }

        await server.Gate.WaitAsync();
        try
        {
            await StopContainerAsync(server);
        }
        finally
        {
            server.Gate.Release();
        }

        return true;
    }

    public async Task StopIdleAsync()
    {
        var cutoff = time.GetUtcNow() - TimeSpan.FromMinutes(settings.IdleTimeoutMinutes);
        foreach (var (name, server) in servers)
        {
            if ((server.LastUsedAt ?? server.StartedAt) < cutoff)
            {
                logger.LogInformation("Stopping idle MCP server {Server}", name);
                await StopAsync(name);
            }
        }
    }

    /// <summary>
    /// Removes MCP server containers left behind by an earlier run (their sessions died with that process).
    /// </summary>
    public async Task ReapOrphansAsync(CancellationToken ct)
    {
        var orphans = await docker.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [$"{ContainerConfigBuilder.ServiceLabel}={ContainerConfigBuilder.McpServiceValue}"] = true }
            }
        }, ct);

        foreach (var orphan in orphans)
        {
            logger.LogInformation("Removing orphaned MCP server container {ContainerId}", ShortId(orphan.ID));
            await RemoveContainerAsync(orphan.ID);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var name in servers.Keys)
        {
            await StopAsync(name);
        }
    }

    /// <summary>
    /// The server's MCP client, restarting its container from the remembered spec if it died; null when the Sandbox
    /// does not know the server.
    /// </summary>
    private async Task<McpClient?> GetClientAsync(string name, CancellationToken ct)
    {
        if (!servers.TryGetValue(name, out var server))
        {
            return null;
        }

        if (!server.IsRunning)
        {
            await server.Gate.WaitAsync(ct);
            try
            {
                if (!server.IsRunning && server.Spec is { } spec)
                {
                    logger.LogInformation("MCP server {Server} is not running; restarting it", name);
                    await StopContainerAsync(server);
                    await StartAsync(server, spec, HashSpec(spec), ct);
                }
            }
            finally
            {
                server.Gate.Release();
            }
        }

        server.LastUsedAt = time.GetUtcNow();
        return server.Client;
    }

    private async Task StartAsync(HostedServer server, McpServerLaunchSpec spec, string hash, CancellationToken ct)
    {
        string? containerId = null;
        ContainerStdioBridge? bridge = null;
        var started = false;

        try
        {
            if (settings.PullImageIfMissing)
            {
                await images.EnsureImageAsync(spec.Image, ct);
            }

            var created = await docker.Containers.CreateContainerAsync(configs.BuildMcp(server.Name, spec), ct);
            containerId = created.ID;

            // Attach before starting so no early output is missed; stdin needs the hijacked attach.
            var stdio = await attacher.AttachAsync(containerId, ct);
            bridge = new ContainerStdioBridge(stdio, settings.MaxMessageBytes, settings.StderrTailLines);

            if (!await docker.Containers.StartContainerAsync(containerId, null, ct))
            {
                throw new McpServerStartException($"Docker refused to start the container for MCP server '{server.Name}'.");
            }

            using var initTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            initTimeout.CancelAfter(TimeSpan.FromSeconds(settings.InitializationTimeoutSeconds));

            var transport = new StreamClientTransport(bridge.ServerInput, bridge.ServerOutput, loggerFactory);
            var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions { ClientInfo = ClientInfo, InitializationTimeout = TimeSpan.FromSeconds(settings.InitializationTimeoutSeconds) },
                loggerFactory,
                initTimeout.Token);

            server.ContainerId = containerId;
            server.Bridge = bridge;
            server.Client = client;
            server.SpecHash = hash;
            server.StartedAt = time.GetUtcNow();
            server.LastUsedAt = server.StartedAt;
            server.LastError = null;
            started = true;

            logger.LogInformation("Started MCP server {Server} ({Image}) in container {ContainerId}", server.Name, spec.Image, ShortId(containerId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            if (bridge is not null)
            {
                // A server that died on startup usually said why on stderr; let the pump drain it.
                await Task.WhenAny(bridge.Completion, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None));
            }

            var stderr = bridge?.StderrTail is { Count: > 0 } tail ? $" Last output: {string.Join(" | ", tail.TakeLast(5))}" : string.Empty;
            server.LastError = $"{ex.Message}{stderr}";
            server.LastStderr = bridge?.StderrTail ?? [];

            logger.LogWarning(ex, "Could not start MCP server {Server}", server.Name);
            throw new McpServerStartException($"MCP server '{server.Name}' could not be started: {server.LastError}", ex);
        }
        finally
        {
            // Also when the caller gave up (cancellation): never leave a half-started container behind.
            if (!started)
            {
                if (bridge is not null)
                {
                    await bridge.DisposeAsync();
                }

                if (containerId is not null)
                {
                    await RemoveContainerAsync(containerId);
                }
            }
        }
    }

    private async Task StopContainerAsync(HostedServer server)
    {
        if (server.Client is { } client)
        {
            server.Client = null;
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Error closing the MCP session of {Server}", server.Name);
            }
        }

        if (server.Bridge is { } bridge)
        {
            server.LastStderr = bridge.StderrTail;
            server.Bridge = null;
            await bridge.DisposeAsync();
        }

        if (server.ContainerId is { } containerId)
        {
            server.ContainerId = null;
            await RemoveContainerAsync(containerId);
        }

        server.SpecHash = null;
    }

    private async Task RemoveContainerAsync(string containerId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await docker.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true, RemoveVolumes = true }, cts.Token);
        }
        catch (DockerContainerNotFoundException)
        {
            // Already gone.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogWarning(ex, "Failed to remove MCP container {ContainerId}; it may persist on the host.", ShortId(containerId));
        }
    }

    private void CheckAllowed(string name, McpServerLaunchSpec spec)
    {
        if (!settings.Enabled)
        {
            throw new McpHostRejectedException("The Sandbox MCP host is disabled (Sandbox:Mcp:Enabled).");
        }

        if (settings.RequireDigestPinnedImages && !DigestPinned().IsMatch(spec.Image))
        {
            throw new McpHostRejectedException($"Image '{spec.Image}' must be pinned by digest (repo@sha256:...).");
        }

        if (!string.IsNullOrWhiteSpace(spec.Network) && !settings.AllowedNetworks.Contains(spec.Network, StringComparer.Ordinal))
        {
            throw new McpHostRejectedException($"Network '{spec.Network}' is not allowed by the Sandbox operator (Sandbox:Mcp:AllowedNetworks).");
        }

        if (!servers.ContainsKey(name) && servers.Count >= settings.MaxServers)
        {
            throw new McpHostRejectedException($"The Sandbox already runs the maximum of {settings.MaxServers} MCP servers.");
        }
    }

    private static string HashSpec(McpServerLaunchSpec spec)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            spec.Image,
            Args = spec.Args ?? [],
            Env = (spec.Env ?? new Dictionary<string, string>()).OrderBy(e => e.Key, StringComparer.Ordinal).ToArray(),
            spec.Network,
            spec.MemoryMb,
            spec.CpuLimit,
        });

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ShortId(string id) => id.Length >= 12 ? id[..12] : id;

    [GeneratedRegex(@"@sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPinned();

    private sealed class HostedServer(string name)
    {
        public string Name { get; } = name;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public McpServerLaunchSpec? Spec { get; set; }

        public string? SpecHash { get; set; }

        public string? ContainerId { get; set; }

        public ContainerStdioBridge? Bridge { get; set; }

        public McpClient? Client { get; set; }

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? LastUsedAt { get; set; }

        public string? LastError { get; set; }

        public IReadOnlyList<string> LastStderr { get; set; } = [];

        public bool IsRunning => Client is not null && !Client.Completion.IsCompleted && Bridge is { Completion.IsCompleted: false };

        public McpServerStatus ToStatus()
        {
            return new(
                Name,
                IsRunning,
                ContainerId,
                StartedAt,
                LastUsedAt,
                LastError,
                Bridge?.StderrTail ?? LastStderr);
        }
    }
}
