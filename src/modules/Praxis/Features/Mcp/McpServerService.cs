using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Core.Results;
using Sentium.Infrastructure.Net;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Validation;

namespace Sentium.Praxis.Features.Mcp;

public interface IMcpServerService
{
    Task<IReadOnlyList<McpServerDto>> GetAllAsync(CancellationToken ct);

    Task<McpServerDto?> GetAsync(Guid id, CancellationToken ct);

    Task<Result<McpServerDto>> CreateAsync(CreateMcpServerRequest request, CancellationToken ct);

    Task<Result<McpServerDto>> UpdateAsync(Guid id, UpdateMcpServerRequest request, CancellationToken ct);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Connects to the server and refreshes its tool list. New tools start disabled and unapproved; tools whose
    /// definition changed lose their approval until re-approved.
    /// </summary>
    Task<Result<McpServerDto>> SyncAsync(Guid id, CancellationToken ct);

    Task<Result<McpServerDto>> UpdateToolsAsync(Guid id, UpdateMcpToolsRequest request, CancellationToken ct);

    Task<McpServerStatusDto?> GetStatusAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Sovereign-only management of MCP servers and their tool approvals. Every change rebuilds the in-memory
/// <see cref="IMcpToolCatalog"/> and drops the server's live connection, so agents pick up the change on their next run.
/// </summary>
public sealed class McpServerService(
    IMcpServerRepository repository,
    IMcpToolCatalog catalog,
    McpClientManager clients,
    McpSecretProtector secrets,
    ISandboxMcpHostClient sandbox,
    HybridCache cache,
    TimeProvider time,
    ILogger<McpServerService> logger) : IMcpServerService
{
    public async Task<IReadOnlyList<McpServerDto>> GetAllAsync(CancellationToken ct)
    {
        var servers = await repository.GetAllAsync(ct);
        return [.. servers.Select(ToDto)];
    }

    public async Task<McpServerDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var server = await repository.GetTrackedAsync(id, ct);
        return server is null ? null : ToDto(server);
    }

    public async Task<Result<McpServerDto>> CreateAsync(CreateMcpServerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await repository.NameExistsAsync(request.Name, ct))
        {
            return Result<McpServerDto>.Conflict($"An MCP server named '{request.Name}' already exists.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var server = new McpServer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim(),
            Transport = request.Transport,
            Endpoint = request.Transport == McpTransport.Http ? request.Endpoint?.Trim() : null,
            Image = request.Transport == McpTransport.SandboxStdio ? request.Image?.Trim() : null,
            Args = request.Transport == McpTransport.SandboxStdio ? [.. request.Args ?? []] : [],
            Network = request.Transport == McpTransport.SandboxStdio ? NullIfBlank(request.Network) : null,
            MemoryMb = request.MemoryMb,
            CpuLimit = request.CpuLimit,
            ProtectedSecrets = secrets.Protect(request.Secrets ?? new Dictionary<string, string>()),
            Variables = new Dictionary<string, string>(request.Variables ?? new Dictionary<string, string>(), StringComparer.Ordinal),
            Enabled = request.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.AddAsync(server, ct);
        await catalog.RefreshAsync(ct);

        logger.LogInformation("MCP server '{Server}' ({Transport}) registered", server.Name, server.Transport);
        return Result<McpServerDto>.Success(ToDto(server));
    }

    public async Task<Result<McpServerDto>> UpdateAsync(Guid id, UpdateMcpServerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var server = await repository.GetTrackedAsync(id, ct);
        if (server is null)
        {
            return Result<McpServerDto>.NotFound();
        }

        if (McpRequestRules.ValidateForTransport(server.Transport, request) is { } invalid)
        {
            return Result<McpServerDto>.Invalid(invalid);
        }

        IReadOnlyDictionary<string, string> currentSecrets;
        try
        {
            currentSecrets = secrets.Unprotect(server.ProtectedSecrets);
        }
        catch (CryptographicException)
        {
            currentSecrets = new Dictionary<string, string>();
        }

        var mergedSecrets = new Dictionary<string, string>(currentSecrets, StringComparer.Ordinal);
        var mergedVariables = new Dictionary<string, string>(server.Variables, StringComparer.Ordinal);

        foreach (var (name, value) in request.Secrets ?? new Dictionary<string, string?>())
        {
            if (value is null)
            {
                mergedSecrets.Remove(name);
            }
            else
            {
                mergedSecrets[name] = value;
                mergedVariables.Remove(name);
            }
        }

        foreach (var (name, value) in request.Variables ?? new Dictionary<string, string?>())
        {
            if (value is null)
            {
                mergedVariables.Remove(name);
            }
            else
            {
                mergedVariables[name] = value;
                mergedSecrets.Remove(name);
            }
        }

        server.DisplayName = request.DisplayName.Trim();
        server.Description = request.Description?.Trim();
        server.Endpoint = server.Transport == McpTransport.Http ? request.Endpoint?.Trim() : null;
        server.Image = server.Transport == McpTransport.SandboxStdio ? request.Image?.Trim() : null;
        server.Args = server.Transport == McpTransport.SandboxStdio ? [.. request.Args ?? []] : [];
        server.Network = server.Transport == McpTransport.SandboxStdio ? NullIfBlank(request.Network) : null;
        server.MemoryMb = request.MemoryMb;
        server.CpuLimit = request.CpuLimit;
        server.ProtectedSecrets = secrets.Protect(mergedSecrets);
        server.Variables = mergedVariables;
        server.Enabled = request.Enabled;
        server.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await repository.SaveChangesAsync(ct);
        await clients.CloseAsync(server.Id);
        await catalog.RefreshAsync(ct);

        return Result<McpServerDto>.Success(ToDto(server));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var server = await repository.GetTrackedAsync(id, ct);
        if (server is null)
        {
            return false;
        }

        var affectedOwners = await repository.DeleteAsync(server, ct);
        await clients.CloseAsync(id);
        await catalog.RefreshAsync(ct);

        foreach (var scope in affectedOwners.Select(o => o.ToString()).Append("sovereign"))
        {
            await cache.RemoveByTagAsync($"{AgentService.CacheTag}:{scope}", ct);
        }

        logger.LogInformation("MCP server '{Server}' deleted; tool assignments removed for {Owners} user(s)", server.Name, affectedOwners.Count);
        return true;
    }

    public async Task<Result<McpServerDto>> SyncAsync(Guid id, CancellationToken ct)
    {
        var server = await repository.GetTrackedAsync(id, ct);
        if (server is null)
        {
            return Result<McpServerDto>.NotFound();
        }

        IReadOnlyList<ModelContextProtocol.Protocol.Tool> discovered;
        try
        {
            discovered = await clients.ListToolsAsync(McpServerConnectionInfo.From(server), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            server.LastError = Describe(ex);
            server.LastSyncedAt = time.GetUtcNow();
            await repository.SaveChangesAsync(ct);

            return Result<McpServerDto>.Invalid($"Could not list the tools of '{server.Name}': {server.LastError}");
        }

        McpToolMerger.Merge(server, discovered);
        server.LastError = null;
        server.LastSyncedAt = time.GetUtcNow();

        await repository.SaveChangesAsync(ct);
        await catalog.RefreshAsync(ct);

        return Result<McpServerDto>.Success(ToDto(server));
    }

    public async Task<Result<McpServerDto>> UpdateToolsAsync(Guid id, UpdateMcpToolsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var server = await repository.GetTrackedAsync(id, ct);
        if (server is null)
        {
            return Result<McpServerDto>.NotFound();
        }

        var byName = server.Tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var unknown = request.Tools.Where(u => !byName.ContainsKey(u.Name)).Select(u => u.Name).ToList();
        if (unknown.Count > 0)
        {
            return Result<McpServerDto>.Invalid($"Unknown tool(s) on '{server.Name}': {string.Join(", ", unknown)}.");
        }

        foreach (var update in request.Tools)
        {
            var tool = byName[update.Name];

            if (update.Approve == true)
            {
                tool.ApprovedHash = tool.DefinitionHash;
                tool.ApprovedDescription = tool.Description;
            }

            if (update.Enabled is { } enabled)
            {
                tool.Enabled = enabled;
            }

            if (update.RequiresApproval is { } requiresApproval)
            {
                tool.RequiresApproval = requiresApproval;
            }
        }

        await repository.SaveChangesAsync(ct);
        await catalog.RefreshAsync(ct);

        return Result<McpServerDto>.Success(ToDto(server));
    }

    public async Task<McpServerStatusDto?> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var server = await repository.GetTrackedAsync(id, ct);
        if (server is null)
        {
            return null;
        }

        SandboxMcpServerStatus? sandboxStatus = null;
        string? sandboxError = null;

        if (server.Transport == McpTransport.SandboxStdio)
        {
            try
            {
                sandboxStatus = await sandbox.GetStatusAsync(server.Name, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or SandboxMcpServerUnknownException)
            {
                sandboxError = ex.Message;
            }
        }

        return new McpServerStatusDto(clients.GetState(id), sandboxStatus, sandboxError);
    }

    private McpServerDto ToDto(McpServer server) => new(
        server.Id,
        server.Name,
        server.DisplayName,
        server.Description,
        server.Transport,
        server.Endpoint,
        server.Image,
        server.Args,
        server.Network,
        server.MemoryMb,
        server.CpuLimit,
        secrets.GetNames(server.ProtectedSecrets),
        new SortedDictionary<string, string>(server.Variables, StringComparer.Ordinal),
        server.Enabled,
        [.. server.Tools.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => ToDto(server, t))],
        server.LastSyncedAt,
        server.LastError,
        clients.GetState(server.Id),
        server.CreatedAt,
        server.UpdatedAt);

    private static McpToolDto ToDto(McpServer server, McpToolEntry tool) => new(
        tool.Name,
        tool.QualifiedName,
        tool.Title,
        tool.Description,
        tool.ApprovedDescription,
        tool.InputSchema,
        tool.ReadOnlyHint,
        tool.DestructiveHint,
        tool.IdempotentHint,
        tool.OpenWorldHint,
        tool.Enabled,
        tool.RequiresApproval,
        Approved: tool.ApprovedHash is not null && tool.ApprovedHash == tool.DefinitionHash,
        Changed: tool.ApprovedHash is not null && tool.ApprovedHash != tool.DefinitionHash,
        tool.Missing,
        Usable: McpToolCatalog.IsUsable(server, tool));

    private static string Describe(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is NetworkAddressBlockedException blocked)
            {
                return blocked.Message;
            }
        }

        return ex is OperationCanceledException ? "The server did not respond in time." : ex.Message;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
