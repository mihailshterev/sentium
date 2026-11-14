using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// Shared formatting for tool calls and results, so the live stream and the persisted timeline agree.
/// </summary>
public static class AgentActivityFormat
{
    /// <summary>
    /// Cap for argument JSON and result previews sent to the UI.
    /// </summary>
    public const int MaxPayloadChars = 2000;

    /// <summary>
    /// Prefix <c>SentinelFunctionMiddleware</c> puts on a policy denial returned as the function result.
    /// </summary>
    public const string DeniedPrefix = "[Access Denied by Security Policy]";

    /// <summary>
    /// camelCase options for frame payloads (the portal reads them as plain JSON).
    /// </summary>
    public static readonly JsonSerializerOptions FrameJson = new(JsonSerializerDefaults.Web);

    public static string? Arguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        try
        {
            return Cap(JsonSerializer.Serialize(arguments, AIJsonUtilities.DefaultOptions));
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException)
        {
            return Cap(string.Join(", ", arguments.Select(kv => $"{kv.Key}={kv.Value}")));
        }
    }

    public static string? Result(FunctionResultContent result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Exception is { } ex)
        {
            return Cap(ex.Message);
        }

        return result.Result switch
        {
            null => null,
            string s => Cap(s),
            JsonElement { ValueKind: JsonValueKind.String } e => Cap(e.GetString() ?? string.Empty),
            var other => TrySerialize(other),
        };
    }

    public static string Status(FunctionResultContent result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Exception is not null)
        {
            return ToolStatuses.Error;
        }

        var text = result.Result switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
            _ => null,
        };

        return text is not null && text.StartsWith(DeniedPrefix, StringComparison.Ordinal) ? ToolStatuses.Denied : ToolStatuses.Ok;
    }

    public static string Serialize<T>(T frame) => JsonSerializer.Serialize(frame, FrameJson);

    private static string? TrySerialize(object value)
    {
        try
        {
            return Cap(JsonSerializer.Serialize(value, AIJsonUtilities.DefaultOptions));
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException)
        {
            return Cap(value.ToString() ?? string.Empty);
        }
    }

    private static string Cap(string value) => value.Length > MaxPayloadChars ? string.Concat(value.AsSpan(0, MaxPayloadChars), "…") : value;
}
