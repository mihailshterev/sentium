using System.Collections.Concurrent;
using System.Net;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sentium.Mcp.Entities;
using Sentium.Mcp.Features.Servers;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Owns the long-lived MCP connections (one per server): connects lazily on first use, reconnects when the session has
/// ended or the server's configuration changed, and closes idle connections - which also stops a Sandbox-hosted server's
/// container. Tool calls are never retried on a live connection (they may not be idempotent); only discovery is.
/// </summary>
internal sealed class McpClientManager(
    IHttpClientFactory httpClientFactory,
    ISandboxMcpHostClient sandbox,
    McpSecretProtector secrets,
    IMcpSettingsProvider settings,
    ILoggerFactory loggerFactory,
    ILogger<McpClientManager> logger) : IMcpToolInvoker, IAsyncDisposable
{
    /// <summary>
    /// Named HttpClient for direct (HTTP) MCP servers; its primary handler only connects to local-network addresses.
    /// </summary>
    public const string HttpClientName = "praxis-mcp";

    private static readonly Implementation ClientInfo = new() { Name = "Sentium", Version = "1.0.0" };

    private readonly ConcurrentDictionary<Guid, Slot> slots = new();

    public Task<IReadOnlyList<Tool>> ListToolsAsync(McpServerConnectionInfo server, CancellationToken ct) =>
        UseAsync(server, (connection, token) => connection.ListToolsAsync(token), retryOnFailure: true, ct);

    public Task<CallToolResult> CallToolAsync(
        McpServerConnectionInfo server,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken ct) =>
        UseAsync(server, (connection, token) => connection.CallToolAsync(toolName, arguments, token), retryOnFailure: false, ct);

    public McpConnectionState GetState(Guid serverId)
    {
        return slots.TryGetValue(serverId, out var slot)
            ? new McpConnectionState(slot.Connection is { IsAlive: true }, slot.ConnectedAt, slot.LastUsedAt, slot.LastError)
            : new McpConnectionState(false, null, null, null);
    }

    /// <summary>
    /// Closes the server's connection (e.g. after it was edited, disabled or deleted).
    /// </summary>
    public async Task CloseAsync(Guid serverId)
    {
        if (slots.TryRemove(serverId, out var slot))
        {
            await slot.DisposeConnectionAsync(logger);
        }
    }

    public async Task CloseAllAsync()
    {
        foreach (var id in slots.Keys)
        {
            await CloseAsync(id);
        }
    }

    /// <summary>
    /// Closes connections unused for longer than <paramref name="idleTimeout"/>.
    /// </summary>
    public async Task CloseIdleAsync(TimeSpan idleTimeout)
    {
        var cutoff = DateTimeOffset.UtcNow - idleTimeout;
        foreach (var (id, slot) in slots)
        {
            if (slot.Connection is not null && (slot.LastUsedAt ?? slot.ConnectedAt) < cutoff)
            {
                logger.LogInformation("Closing idle MCP connection to server {ServerId}", id);
                await slot.DisposeConnectionAsync(logger);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAllAsync();
    }

    private async Task<T> UseAsync<T>(
        McpServerConnectionInfo server,
        Func<IMcpConnection, CancellationToken, Task<T>> operation,
        bool retryOnFailure,
        CancellationToken ct)
    {
        var slot = slots.GetOrAdd(server.Id, _ => new Slot());
        var connection = await GetConnectionAsync(slot, server, ct);

        try
        {
            var result = await operation(connection, ct);
            slot.LastUsedAt = DateTimeOffset.UtcNow;
            slot.LastError = null;
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && IsConnectionLost(connection, ex))
        {
            slot.LastError = ex.Message;
            await slot.DisposeConnectionAsync(logger, connection);

            if (!retryOnFailure)
            {
                throw;
            }

            logger.LogInformation("MCP connection to '{Server}' was lost; reconnecting once", server.Name);
            connection = await GetConnectionAsync(slot, server, ct);

            var result = await operation(connection, ct);
            slot.LastUsedAt = DateTimeOffset.UtcNow;
            slot.LastError = null;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            slot.LastError = ex.Message;
            throw;
        }
    }

    private async Task<IMcpConnection> GetConnectionAsync(Slot slot, McpServerConnectionInfo server, CancellationToken ct)
    {
        if (slot.Connection is { IsAlive: true } current && slot.Version == server.Version)
        {
            return current;
        }

        await slot.Gate.WaitAsync(ct);
        try
        {
            if (slot.Connection is { IsAlive: true } existing && slot.Version == server.Version)
            {
                return existing;
            }

            await slot.DisposeConnectionAsync(logger);

            try
            {
                var connection = await ConnectAsync(server, ct);
                slot.Connection = connection;
                slot.Version = server.Version;
                slot.ConnectedAt = DateTimeOffset.UtcNow;
                slot.LastError = null;
                return connection;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                slot.LastError = ex.Message;
                logger.LogWarning(ex, "Could not connect to MCP server '{Server}'", server.Name);
                throw;
            }
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    private async Task<IMcpConnection> ConnectAsync(McpServerConnectionInfo server, CancellationToken ct)
    {
        var runtime = await settings.GetAsync(ct);
        var connectTimeout = TimeSpan.FromSeconds(runtime.ConnectTimeoutSeconds);

        // Secrets and plain variables are the same thing to the server: headers (HTTP) or environment (stdio).
        var secretValues = new Dictionary<string, string>(server.Variables, StringComparer.Ordinal);
        foreach (var (name, value) in secrets.Unprotect(server.ProtectedSecrets))
        {
            secretValues[name] = value;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(connectTimeout);

        switch (server.Transport)
        {
            case McpTransport.Http:
            {
                var endpoint = server.Endpoint is { Length: > 0 } e ? new Uri(e) : throw new InvalidOperationException($"MCP server '{server.Name}' has no endpoint.");

                // Disposed in the finally below unless ownership passed to the connection (CA2000 does not recognise
                // DisposeAsync there).
#pragma warning disable CA2000
                var transport = new HttpClientTransport(
                    new HttpClientTransportOptions
                    {
                        Endpoint = endpoint,
                        Name = server.Name,
                        TransportMode = HttpTransportMode.AutoDetect,
                        ConnectionTimeout = connectTimeout,
                        AdditionalHeaders = new Dictionary<string, string>(secretValues, StringComparer.OrdinalIgnoreCase),
                    },
                    httpClientFactory.CreateClient(HttpClientName),
                    loggerFactory,
                    ownsHttpClient: true);
#pragma warning restore CA2000

                try
                {
                    var client = await McpClient.CreateAsync(
                        transport,
                        new McpClientOptions { ClientInfo = ClientInfo, InitializationTimeout = connectTimeout },
                        loggerFactory,
                        timeout.Token);

                    var connection = new DirectMcpConnection(client, transport);
                    transport = null;

                    logger.LogInformation("Connected to MCP server '{Server}' at {Endpoint}", server.Name, endpoint);
                    return connection;
                }
                finally
                {
                    if (transport is not null)
                    {
                        await transport.DisposeAsync();
                    }
                }
            }

            case McpTransport.SandboxStdio:
            {
                var image = server.Image is { Length: > 0 } i ? i : throw new InvalidOperationException($"MCP server '{server.Name}' has no image.");
                var connection = new SandboxMcpConnection(
                    sandbox,
                    server.Name,
                    new SandboxMcpLaunchSpec(image, server.Args, secretValues, server.Network, server.MemoryMb, server.CpuLimit));

                await connection.EnsureAsync(timeout.Token);

                logger.LogInformation("Started Sandbox-hosted MCP server '{Server}' ({Image})", server.Name, image);
                return connection;
            }

            default:
                throw new InvalidOperationException($"Unsupported MCP transport '{server.Transport}'.");
        }
    }

    private static bool IsConnectionLost(IMcpConnection connection, Exception ex) =>
        !connection.IsAlive || ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound } or ObjectDisposedException;

    private sealed class Slot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public IMcpConnection? Connection { get; set; }

        public DateTime Version { get; set; }

        public DateTimeOffset? ConnectedAt { get; set; }

        public DateTimeOffset? LastUsedAt { get; set; }

        public string? LastError { get; set; }

        public async Task DisposeConnectionAsync(ILogger logger, IMcpConnection? expected = null)
        {
            var connection = Connection;
            if (connection is null || (expected is not null && !ReferenceEquals(connection, expected)))
            {
                return;
            }

            Connection = null;
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Error while closing an MCP connection");
            }
        }
    }
}
