using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill for analysing and transforming JSON data.
/// </summary>
internal sealed class JsonAnalyzerSkill : AgentClassSkill<JsonAnalyzerSkill>
{
    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "json-analyzer",
        "Analyse, validate, pretty-print, and summarise JSON structures. Use when the user provides JSON data or asks about JSON schemas, validation, or transformation.",
        """
        Use this skill when the user provides a JSON payload or asks about JSON.

        1. Use the validate script to check whether the input is valid JSON.
        2. If valid, use the summarize script to describe the top-level structure.
        3. For pretty printing, use the pretty-print script.
        4. Explain the structure clearly: list top-level keys, value types, and array lengths.
        5. If the JSON is invalid, report the error message and location.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "json-analyzer",
        "Analyse, validate, pretty-print, and summarise JSON structures. Use when the user provides JSON data or asks about JSON schemas, validation, or transformation.");

    protected override string Instructions => """
        Use this skill when the user provides a JSON payload or asks about JSON.

        1. Use the validate script to check whether the input is valid JSON.
        2. If valid, use the summarize script to describe the top-level structure.
        3. For pretty printing, use the pretty-print script.
        4. Explain the structure clearly: list top-level keys, value types, and array lengths.
        5. If the JSON is invalid, report the error message and location.
        """;

    [AgentSkillResource("json-tips")]
    [Description("Common JSON best practices and pitfalls.")]
    public string JsonTips => """
        # JSON Best Practices

        ## Structure
        - Use camelCase for property names (JavaScript convention) or snake_case (Python/API convention) - be consistent.
        - Prefer flat structures; deeply nested JSON is hard to maintain.
        - Use arrays for homogeneous collections.

        ## Types
        - Use native JSON types: string, number, boolean, null, array, object.
        - Represent dates as ISO 8601 strings: "2026-05-09T12:00:00Z".
        - Avoid using strings for numeric values unless precision requires it.

        ## Schema
        - Define a JSON Schema to validate payloads at system boundaries.
        - Validate on both ingestion and egress.

        ## Common Pitfalls
        - Trailing commas are NOT allowed in standard JSON.
        - Comments are NOT allowed in standard JSON (use JSONC or JSON5 for dev configs).
        - Duplicate keys have undefined behaviour - avoid them.
        """;

    [AgentSkillScript("validate")]
    [Description("Validates a JSON string and returns whether it is valid and any parse error.")]
    private static string ValidateJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(new { valid = true, error = (string?)null });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { valid = false, error = ex.Message });
        }
    }

    [AgentSkillScript("summarize")]
    [Description("Parses a JSON string and returns a summary of its top-level structure.")]
    private static string SummarizeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var keys = root.EnumerateObject().Select(p => new { key = p.Name, type = p.Value.ValueKind.ToString() }).ToArray();
                return JsonSerializer.Serialize(new { rootType = "object", keyCount = keys.Length, keys });
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                var count = root.GetArrayLength();
                var itemType = count > 0 ? root[0].ValueKind.ToString() : "unknown";
                return JsonSerializer.Serialize(new { rootType = "array", count, itemType });
            }

            return JsonSerializer.Serialize(new { rootType = root.ValueKind.ToString() });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [AgentSkillScript("pretty-print")]
    [Description("Pretty-prints a compact JSON string with indentation.")]
    private static string PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException ex)
        {
            return $"Invalid JSON: {ex.Message}";
        }
    }
}
