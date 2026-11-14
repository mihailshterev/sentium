using System.Text;
using ModelContextProtocol.Protocol;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Renders a <see cref="CallToolResult"/> as plain text for the model.
/// </summary>
internal static class McpResultFormatter
{
    public static string Format(CallToolResult result, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(block switch
            {
                TextContentBlock textBlock => textBlock.Text,
                ImageContentBlock image => $"[image: {image.MimeType}]",
                AudioContentBlock audio => $"[audio: {audio.MimeType}]",
                ResourceLinkBlock link => $"[resource: {link.Name} {link.Uri}]",
                EmbeddedResourceBlock embedded => embedded.Resource is TextResourceContents textResource ? textResource.Text : $"[resource: {embedded.Resource.Uri}]",
                _ => $"[{block.Type} content]",
            });
        }

        if (sb.Length == 0 && result.StructuredContent is { } structured)
        {
            sb.Append(structured.GetRawText());
        }

        var text = sb.Length == 0 ? "(no content)" : sb.ToString();
        if (text.Length > maxChars)
        {
            text = string.Concat(text.AsSpan(0, maxChars), $"\n[truncated: the result exceeded {maxChars} characters]");
        }

        return result.IsError == true ? $"[MCP tool error] {text}" : text;
    }
}
