using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Mcp.Features.Servers;
using Sentium.Praxis.Contracts;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// An MCP tool as an agent sees it. Name, description and input schema come from the admin-approved snapshot, never from
/// the live server, so a server cannot change what the model is told a tool does without re-approval. It is always
/// classified <see cref="ToolResource.External"/> / <see cref="ToolAction.Execute"/>: every call is evaluated by Sentinel,
/// with its arguments scanned for sensitive data before they leave Sentium.
/// </summary>
/// <remarks>
/// The call goes through the MCP SDK's <c>CallToolAsync</c> rather than an <c>McpClientTool</c>: agents are built per
/// request and must not wait for (or fail on) a server connection unless the tool is actually called, and the SDK tool's
/// result is the raw <c>CallToolResult</c> JSON, which small local models handle worse than plain text.
/// </remarks>
internal sealed class McpToolFunction : AIFunction
{
    private readonly McpServerConnectionInfo server;
    private readonly string toolName;
    private readonly IMcpToolInvoker clients;
    private readonly IMcpSettingsProvider settings;
    private readonly IReadOnlySet<string>? declaredArguments;

    public McpToolFunction(
        McpServerConnectionInfo server,
        string qualifiedName,
        string toolName,
        string? description,
        string inputSchema,
        IMcpToolInvoker clients,
        IMcpSettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        this.server = server;
        this.toolName = toolName;
        this.clients = clients;
        this.settings = settings;

        Name = qualifiedName;
        Description = description ?? string.Empty;
        JsonSchema = McpJson.ParseSchema(inputSchema);
        declaredArguments = ReadDeclaredArguments(JsonSchema);
        AdditionalProperties = AgentToolFunction.Classification(ToolResource.External, ToolAction.Execute, $"mcp:{server.Name}/{toolName}");
    }

    public override string Name { get; }

    public override string Description { get; }

    public override JsonElement JsonSchema { get; }

    public override JsonSerializerOptions JsonSerializerOptions => AIJsonUtilities.DefaultOptions;

    public override IReadOnlyDictionary<string, object?> AdditionalProperties { get; }

    public string ServerName => server.Name;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var runtime = await settings.GetAsync(cancellationToken);
        if (!runtime.Enabled)
        {
            return "[MCP tool error] MCP tools are disabled by the administrator.";
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(runtime.CallTimeoutSeconds));

        try
        {
            var result = await clients.CallToolAsync(server, toolName, FilterArguments(arguments), timeout.Token);
            return McpResultFormatter.Format(result, runtime.MaxResultChars);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return $"[MCP tool error] '{Name}' did not answer within {runtime.CallTimeoutSeconds} seconds.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"[MCP tool error] '{Name}' failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Drops arguments the approved schema does not declare (small models hallucinate extra ones). Schemas without
    /// declared properties pass everything through.
    /// </summary>
    private Dictionary<string, object?> FilterArguments(AIFunctionArguments arguments)
    {
        var filtered = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in arguments)
        {
            if (declaredArguments is null || declaredArguments.Contains(key))
            {
                filtered[key] = value;
            }
        }

        return filtered;
    }

    private static HashSet<string>? ReadDeclaredArguments(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind != JsonValueKind.False)
        {
            return null;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.EnumerateObject())
        {
            names.Add(property.Name);
        }

        return names.Count == 0 ? null : names;
    }
}
