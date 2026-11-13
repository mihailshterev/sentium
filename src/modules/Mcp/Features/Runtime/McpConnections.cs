using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sentium.Mcp.Features.Servers;

namespace Sentium.Mcp.Features.Runtime;

internal interface IMcpConnection : IAsyncDisposable
{
    bool IsAlive { get; }

    Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct);

    Task<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct);
}

/// <summary>
/// A Streamable HTTP server reached directly with the MCP SDK's <see cref="McpClient"/>. Owns the client and its
/// transport (and through it the HttpClient).
/// </summary>
internal sealed class DirectMcpConnection(McpClient client, IAsyncDisposable transport) : IMcpConnection
{
    public bool IsAlive => !client.Completion.IsCompleted;

    public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct)
    {
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return [.. tools.Select(t => t.ProtocolTool)];
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct) =>
        await client.CallToolAsync(toolName, arguments, cancellationToken: ct);

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
        await transport.DisposeAsync();
    }
}

/// <summary>
/// A stdio server running in a Sentium.Sandbox container. The Sandbox keeps the stdio session; this side speaks its
/// REST host, re-ensuring the container once when the Sandbox no longer knows it (e.g. after a Sandbox restart).
/// </summary>
internal sealed class SandboxMcpConnection(ISandboxMcpHostClient sandbox, string name, SandboxMcpLaunchSpec spec) : IMcpConnection
{
    public bool IsAlive => true;

    public Task EnsureAsync(CancellationToken ct) => sandbox.EnsureAsync(name, spec, ct);

    public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct)
    {
        var result = await WithEnsureAsync(token => sandbox.ListToolsAsync(name, token), ct);
        return [.. result.Tools];
    }

    public Task<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct)
    {
        var request = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = arguments.ToDictionary(
                a => a.Key,
                a => System.Text.Json.JsonSerializer.SerializeToElement(a.Value, ModelContextProtocol.McpJsonUtilities.DefaultOptions)),
        };

        return WithEnsureAsync(token => sandbox.CallToolAsync(name, request, token), ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await sandbox.StopAsync(name, cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or SandboxMcpServerUnknownException)
        {
            // Best effort: the Sandbox reaps idle and orphaned MCP containers on its own.
        }
    }

    private async Task<T> WithEnsureAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(ct);
        }
        catch (SandboxMcpServerUnknownException)
        {
            await sandbox.EnsureAsync(name, spec, ct);
            return await call(ct);
        }
    }
}
