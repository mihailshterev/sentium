using System.Text.Json.Nodes;
using Sentium.Core.Text;
using Sentium.Memoria.Common;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Sentium.Memoria.Features.Parsing;

/// <summary>
/// Reads Obsidian frontmatter: <c>tags</c>/<c>tag</c> and <c>aliases</c>/<c>alias</c> as lists or comma-separated
/// strings, plus every value as JSON for display. Malformed YAML counts as no frontmatter, never as an error.
/// </summary>
/// <remarks>
/// YAML anchors let a tiny document reference the same node exponentially often, so the JSON copy is built under a
/// node budget and the input is size-capped before parsing.
/// </remarks>
internal static class FrontmatterReader
{
    private const int MaxYamlChars = 64 * 1024;
    private const int MaxJsonNodes = 2_000;
    private const int MaxDepth = 16;

    private static readonly string[] TagKeys = ["tags", "tag"];
    private static readonly string[] AliasKeys = ["aliases", "alias"];

    public static Frontmatter Read(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml) || yaml.Length > MaxYamlChars)
        {
            return Frontmatter.Empty;
        }

        if (Load(yaml) is not { } root)
        {
            return Frontmatter.Empty;
        }

        var tags = ReadStrings(root, TagKeys, splitOnWhitespace: true)
            .Select(t => t.TrimStart('#'))
            .Select(TagNormalizer.NormalizeOne)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MemoriaLimits.TagsPerNoteMax)
            .ToList();

        var aliases = ReadStrings(root, AliasKeys, splitOnWhitespace: false)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MemoriaLimits.AliasesPerNoteMax)
            .ToList();

        return new Frontmatter(tags, aliases, ToJson(root));
    }

    private static YamlMappingNode? Load(string yaml)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));

            return stream.Documents.Count > 0 ? stream.Documents[0].RootNode as YamlMappingNode : null;
        }
        catch (Exception ex) when (ex is YamlException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ReadStrings(YamlMappingNode root, string[] keys, bool splitOnWhitespace)
    {
        char[] separators = splitOnWhitespace ? [',', ' ', '\t'] : [','];

        foreach (var (key, value) in root.Children)
        {
            if (key is not YamlScalarNode { Value: { } name } || !keys.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var scalars = value switch
            {
                YamlSequenceNode sequence => sequence.Children.OfType<YamlScalarNode>().Select(s => s.Value),
                YamlScalarNode scalar => scalar.Value?.Split(separators) ?? [],
                _ => []
            };

            foreach (var item in scalars)
            {
                if (item?.Trim() is { Length: > 0 } trimmed)
                {
                    yield return trimmed;
                }
            }
        }
    }

    private static string? ToJson(YamlMappingNode root)
    {
        var budget = new NodeBudget(MaxJsonNodes);
        var json = ToJsonNode(root, budget, depth: 0);
        return json is JsonObject { Count: > 0 } obj ? obj.ToJsonString() : null;
    }

    private static JsonNode? ToJsonNode(YamlNode node, NodeBudget budget, int depth)
    {
        if (depth > MaxDepth || !budget.TryTake())
        {
            return null;
        }

        switch (node)
        {
            case YamlScalarNode scalar:
                return scalar.Value is { } value ? JsonValue.Create(value) : null;

            case YamlSequenceNode sequence:
                var array = new JsonArray();
                foreach (var child in sequence.Children)
                {
                    array.Add(ToJsonNode(child, budget, depth + 1));
                }

                return array;

            case YamlMappingNode mapping:
                var obj = new JsonObject();
                foreach (var (key, child) in mapping.Children)
                {
                    if (key is YamlScalarNode { Value: { } name })
                    {
                        obj[name] = ToJsonNode(child, budget, depth + 1);
                    }
                }

                return obj;

            default:
                return null;
        }
    }

    private sealed class NodeBudget(int remaining)
    {
        private int _remaining = remaining;

        public bool TryTake() => _remaining-- > 0;
    }
}
