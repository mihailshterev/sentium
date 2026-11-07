using System.Globalization;
using System.Text.Json;

namespace Sentium.Praxis.Features.Tools.Inventory;

/// <summary>
/// Lenient reader for tool arguments: models send JSON objects, bare strings, numbers as strings and
/// booleans as "yes"/"true". A non-JSON input is treated as the value of the tool's primary parameter.
/// </summary>
public sealed class InventoryToolInput
{
    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _raw;

    private InventoryToolInput(string? raw) => _raw = raw;

    public static InventoryToolInput Parse(string? input)
    {
        var text = input?.Trim() ?? string.Empty;

        if (!text.StartsWith('{'))
        {
            return new InventoryToolInput(text.Length == 0 ? null : Unquote(text));
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var parsed = new InventoryToolInput(null);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                parsed._values[property.Name] = property.Value.Clone();
            }

            return parsed;
        }
        catch (JsonException)
        {
            return new InventoryToolInput(text);
        }
    }

    /// <summary>
    /// The named value, or - when the input was not a JSON object - the raw input for the primary parameter.
    /// </summary>
    public string? GetString(string name, bool isPrimary = false)
    {
        if (_values.TryGetValue(name, out var value))
        {
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
                _ => null
            };

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        return isPrimary ? _raw : null;
    }

    public int? GetInt(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null
        };
    }

    public bool? GetBool(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString()?.Trim().ToLowerInvariant() switch
            {
                "true" or "yes" or "1" => true,
                "false" or "no" or "0" => false,
                _ => null
            },
            _ => null
        };
    }

    private static string Unquote(string text)
        => text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1].Trim() : text;
}
