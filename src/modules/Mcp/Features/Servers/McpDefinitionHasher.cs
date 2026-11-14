using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

/// <summary>
/// Turns a discovered MCP <see cref="Tool"/> into an <see cref="McpToolEntry"/> definition and hashes it. The hash covers
/// everything the model sees or that changes how a call is treated (name, title, description, input/output schema,
/// annotations) over canonical JSON (recursively sorted keys), so a server reordering keys is not a "change" but a
/// rewritten description or a flipped read-only hint is - and forces re-approval.
/// </summary>
public static class McpDefinitionHasher
{
    /// <summary>
    /// Descriptions longer than this are cut: the full text goes into the tool schema the model sees on every turn.
    /// </summary>
    public const int MaxDescriptionLength = 1_000;

    public static McpToolDefinition Describe(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var inputSchema = Canonicalize(JsonNode.Parse(tool.InputSchema.GetRawText()));
        if (inputSchema is not JsonObject schemaObject || schemaObject["type"] is null)
        {
            inputSchema = new JsonObject { ["type"] = "object" };
        }

        var outputSchema = tool.OutputSchema is { } output ? Canonicalize(JsonNode.Parse(output.GetRawText())) : null;
        var description = Clean(tool.Description, MaxDescriptionLength);
        var title = Clean(tool.Title ?? tool.Annotations?.Title, 200);

        var annotations = tool.Annotations;
        var definition = new JsonObject
        {
            ["name"] = tool.Name,
            ["title"] = title,
            ["description"] = description,
            ["inputSchema"] = inputSchema.DeepClone(),
            ["outputSchema"] = outputSchema?.DeepClone(),
            ["readOnlyHint"] = annotations?.ReadOnlyHint,
            ["destructiveHint"] = annotations?.DestructiveHint,
            ["idempotentHint"] = annotations?.IdempotentHint,
            ["openWorldHint"] = annotations?.OpenWorldHint,
        };

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definition.ToJsonString())));

        return new McpToolDefinition(
            tool.Name,
            title,
            description,
            inputSchema.ToJsonString(),
            annotations?.ReadOnlyHint,
            annotations?.DestructiveHint,
            annotations?.IdempotentHint,
            annotations?.OpenWorldHint,
            hash);
    }

    /// <summary>
    /// Strips control characters (except plain whitespace) and caps the length. Tool metadata comes from an external
    /// process and lands in the system prompt / tool schema.
    /// </summary>
    public static string? Clean(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var sb = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var c in text)
        {
            if (sb.Length >= maxLength)
            {
                break;
            }

            if (!char.IsControl(c) || c is '\n' or '\t')
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Trim();
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        return node switch
        {
            JsonObject obj => new JsonObject(obj
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, Canonicalize(p.Value)))),
            JsonArray array => new JsonArray([.. array.Select(Canonicalize)]),
            null => null,
            _ => JsonNode.Parse(node.ToJsonString()),
        };
    }
}
