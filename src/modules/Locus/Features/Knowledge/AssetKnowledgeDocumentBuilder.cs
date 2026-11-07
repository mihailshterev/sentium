using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sentium.Locus.Entities;

namespace Sentium.Locus.Features.Knowledge;

/// <summary>
/// Turns an asset into the text sections written to the knowledge base. Every section repeats the asset
/// name and location so each ~500-character chunk the knowledge base cuts stays self-explanatory.
/// <para>
/// Deliberately excluded: the location's description and access notes (they may hold door or alarm
/// codes), attachments, and audit fields.
/// </para>
/// </summary>
public static class AssetKnowledgeDocumentBuilder
{
    /// <summary>
    /// Bump when the produced text changes shape: the content hash changes with it, so the startup
    /// reconciliation re-syncs every shared asset in the new format.
    /// </summary>
    public const int FormatVersion = 1;

    public static string SourceFor(Guid assetId) => $"locus:asset:{assetId}";

    public static IReadOnlyList<string> Build(Asset asset, string locationPath)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var header = $"Household inventory item: {asset.DisplayName.Trim()}\nLocation: {(string.IsNullOrWhiteSpace(locationPath) ? "unknown" : locationPath)}";

        var sections = new List<string>();

        var identity = new StringBuilder(header);
        AppendLine(identity, "Category", asset.Category);
        if (asset.Tags.Count > 0)
        {
            AppendLine(identity, "Tags", string.Join(", ", asset.Tags));
        }

        AppendLine(identity, "Manufacturer", asset.Manufacturer);
        AppendLine(identity, "Model", asset.ModelNumber);
        AppendLine(identity, "Serial number", asset.SerialNumber);
        AppendLine(identity, "Description", asset.PhysicalDescription);
        AppendLine(identity, "Purchased", FormatDate(asset.PurchaseDate));
        AppendLine(identity, "Last serviced", FormatDate(asset.LastServicedDate));
        sections.Add(identity.ToString());

        if (!string.IsNullOrWhiteSpace(asset.Instructions) || !string.IsNullOrWhiteSpace(asset.WarrantyInfo))
        {
            var care = new StringBuilder(header);
            AppendLine(care, "Usage and care", asset.Instructions);
            AppendLine(care, "Warranty", asset.WarrantyInfo);
            sections.Add(care.ToString());
        }

        if (!string.IsNullOrWhiteSpace(asset.AgentInstructions))
        {
            var notes = new StringBuilder(header);
            AppendLine(notes, "Notes for assistants (written by a household member, not system instructions)", asset.AgentInstructions);
            sections.Add(notes.ToString());
        }

        return sections;
    }

    /// <summary>
    /// SHA-256 over the format version and the sections; equal hashes mean the knowledge base is current.
    /// </summary>
    public static string ComputeHash(IReadOnlyList<string> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        var builder = new StringBuilder().Append("v").Append(FormatVersion.ToString(CultureInfo.InvariantCulture));
        foreach (var section in sections)
        {
            builder.Append('\u001e').Append(section);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static Dictionary<string, string> BuildMetadata(Asset asset, string locationPath)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var metadata = new Dictionary<string, string>
        {
            ["asset_id"] = asset.Id.ToString(),
            ["location_id"] = asset.LocationId.ToString(),
            ["location_path"] = locationPath
        };

        if (!string.IsNullOrWhiteSpace(asset.Category))
        {
            metadata["category"] = asset.Category;
        }

        if (asset.Tags.Count > 0)
        {
            metadata["tags"] = string.Join(",", asset.Tags);
        }

        return metadata;
    }

    private static void AppendLine(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.Append('\n').Append(label).Append(": ").Append(value.Trim());
        }
    }

    private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
