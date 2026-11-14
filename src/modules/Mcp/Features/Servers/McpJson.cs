using System.Text.Json;

namespace Sentium.Mcp.Features.Servers;

internal static class McpJson
{
    public static JsonElement ParseSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.Clone();
    }
}
