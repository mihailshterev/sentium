using System.Net;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Sentium.Mcp.Features.Servers;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// How Sentium.Sandbox should run a stdio MCP server container. Mirrors the Sandbox's <c>McpServerLaunchSpec</c>.
/// </summary>
public sealed record SandboxMcpLaunchSpec(
    string Image,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env,
    string? Network,
    int? MemoryMb,
    double? CpuLimit);

/// <summary>
/// Runtime state of a Sandbox-hosted MCP server container.
/// </summary>
public sealed record SandboxMcpServerStatus(
    string Name,
    bool Running,
    string? ContainerId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastUsedAt,
    string? LastError,
    IReadOnlyList<string> StderrTail);

/// <summary>
/// The Sandbox does not know the server (never ensured, or the Sandbox restarted) - ensure it and retry.
/// </summary>
public sealed class SandboxMcpServerUnknownException : Exception
{
    public SandboxMcpServerUnknownException()
    {
    }

    public SandboxMcpServerUnknownException(string message) : base(message)
    {
    }

    public SandboxMcpServerUnknownException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// REST client for the Sandbox's MCP host (<c>/mcp/servers/{name}</c>, internal-token authenticated). Tool payloads are
/// the MCP SDK's own protocol types, serialized with <see cref="McpJsonUtilities.DefaultOptions"/>.
/// </summary>
public interface ISandboxMcpHostClient
{
    Task<SandboxMcpServerStatus> EnsureAsync(string name, SandboxMcpLaunchSpec spec, CancellationToken ct);

    Task StopAsync(string name, CancellationToken ct);

    Task<SandboxMcpServerStatus?> GetStatusAsync(string name, CancellationToken ct);

    Task<ListToolsResult> ListToolsAsync(string name, CancellationToken ct);

    Task<CallToolResult> CallToolAsync(string name, CallToolRequestParams request, CancellationToken ct);
}

public sealed class SandboxMcpHostClient(HttpClient httpClient) : ISandboxMcpHostClient
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public async Task<SandboxMcpServerStatus> EnsureAsync(string name, SandboxMcpLaunchSpec spec, CancellationToken ct)
    {
        using var response = await httpClient.PutAsJsonAsync(ServerPath(name), spec, WebOptions, ct);
        await EnsureSuccessAsync(response, name, ct);

        return await response.Content.ReadFromJsonAsync<SandboxMcpServerStatus>(WebOptions, ct)
            ?? throw new InvalidOperationException("Sandbox returned an empty MCP server status.");
    }

    public async Task StopAsync(string name, CancellationToken ct)
    {
        using var response = await httpClient.DeleteAsync(ServerPath(name), ct);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, name, ct);
        }
    }

    public async Task<SandboxMcpServerStatus?> GetStatusAsync(string name, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(ServerPath(name), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, name, ct);
        return await response.Content.ReadFromJsonAsync<SandboxMcpServerStatus>(WebOptions, ct);
    }

    public async Task<ListToolsResult> ListToolsAsync(string name, CancellationToken ct)
    {
        using var response = await httpClient.PostAsync($"{ServerPath(name)}/tools/list", content: null, ct);
        await EnsureSuccessAsync(response, name, ct);

        return await response.Content.ReadFromJsonAsync<ListToolsResult>(McpJsonUtilities.DefaultOptions, ct)
            ?? throw new InvalidOperationException("Sandbox returned an empty tool list.");
    }

    public async Task<CallToolResult> CallToolAsync(string name, CallToolRequestParams request, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync($"{ServerPath(name)}/tools/call", request, McpJsonUtilities.DefaultOptions, ct);
        await EnsureSuccessAsync(response, name, ct);

        return await response.Content.ReadFromJsonAsync<CallToolResult>(McpJsonUtilities.DefaultOptions, ct)
            ?? throw new InvalidOperationException("Sandbox returned an empty tool result.");
    }

    private static string ServerPath(string name) => $"mcp/servers/{Uri.EscapeDataString(name)}";

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string name, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SandboxMcpServerUnknownException($"The Sandbox does not run MCP server '{name}'.");
        }

        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Sandbox MCP host returned {(int)response.StatusCode} for '{name}': {Truncate(detail, 500)}",
            inner: null,
            response.StatusCode);
    }

    private static string Truncate(string text, int max) => text.Length > max ? text[..max] : text;
}
