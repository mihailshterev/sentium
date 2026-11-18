using System.Globalization;
using System.Text;
using Sentium.Memoria.Contracts.Models;

namespace Sentium.Praxis.Features.Tools.Vault;

/// <summary>
/// Renders vault data for the model. Note text is the user's own writing and may contain anything, so it is fenced in
/// <c>&lt;&lt;&lt;</c>/<c>&gt;&gt;&gt;</c> with the markers defanged; every note carries its <c>obsidian://</c> link
/// so answers can cite it.
/// </summary>
internal static class VaultToolFormatter
{
    private const string Citation = "[Cite notes as markdown links using their Open link, e.g. [Title](obsidian://...).]";

    public static string FormatOverview(VaultOverview overview)
    {
        ArgumentNullException.ThrowIfNull(overview);

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"[Vault \"{OneLine(overview.Name, 120)}\"]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Notes: {overview.NoteCount}, links: {overview.LinkCount}, last synced: {overview.LastSyncAt?.ToString("u", CultureInfo.InvariantCulture) ?? "never"}");

        if (overview.TopTags.Count > 0)
        {
            builder.AppendLine("Top tags: " + string.Join(", ", overview.TopTags.Select(t => $"#{OneLine(t.Tag, 60)} ({t.Count})")));
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Orphan notes (no links in or out): {overview.OrphanCount}");
        foreach (var orphan in overview.Orphans)
        {
            builder.Append("- ").AppendLine(Ref(orphan));
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Unresolved links (targets with no note yet): {overview.UnresolvedLinkCount}");
        if (overview.UnresolvedTargets.Count > 0)
        {
            builder.AppendLine("Most linked missing notes: " + string.Join(", ", overview.UnresolvedTargets.Select(t => $"[[{OneLine(t, 80)}]]")));
        }

        if (overview.PendingIndexCount > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Note: {overview.PendingIndexCount} note(s) are still being indexed; search may miss their latest text.");
        }

        return Finish(builder);
    }

    public static string FormatHits(IReadOnlyList<VaultSearchHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"[Vault search - {hits.Count} matching note(s)]");

        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            builder.AppendLine();
            builder.AppendLine(CultureInfo.InvariantCulture, $"{i + 1}. {Ref(hit.Note)} - score {hit.Score:P0}");
            Fence(builder, hit.Snippet);
        }

        builder.AppendLine();
        builder.AppendLine(Citation);
        return Finish(builder);
    }

    public static string FormatNote(VaultNoteDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"[Note {Ref(detail.Note)}]");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Updated: {detail.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)}");

        if (detail.Aliases.Count > 0)
        {
            builder.AppendLine("Aliases: " + string.Join(", ", detail.Aliases.Select(a => OneLine(a, 80))));
        }

        AppendLinks(builder, detail);

        builder.AppendLine(detail.Truncated ? "Content (truncated):" : "Content:");
        Fence(builder, detail.Content);
        builder.AppendLine(Citation);
        return Finish(builder);
    }

    public static string FormatLinks(VaultNoteDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"[Links of {Ref(detail.Note)}]");
        AppendLinks(builder, detail);
        builder.AppendLine(Citation);
        return Finish(builder);
    }

    public static string FormatRelated(VaultNoteRef? note, string heading, IReadOnlyList<VaultRelatedNote> related)
    {
        ArgumentNullException.ThrowIfNull(related);

        var builder = new StringBuilder();
        builder.AppendLine(note is null ? $"[{heading}]" : $"[{heading} {Ref(note)}]");

        if (related.Count == 0)
        {
            builder.AppendLine("Nothing found.");
            return Finish(builder);
        }

        foreach (var group in related.GroupBy(r => r.Relation))
        {
            builder.AppendLine(RelationLabel(group.Key) + ":");
            foreach (var item in group)
            {
                builder.Append("- ").Append(Ref(item.Note)).AppendLine(StrengthLabel(item));
            }
        }

        builder.AppendLine(Citation);
        return Finish(builder);
    }

    public static string OneLine(string value, int max)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Cap(Defang(string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))), max);
    }

    private static void AppendLinks(StringBuilder builder, VaultNoteDetail detail)
    {
        var resolved = detail.Links.Where(l => l.Target is not null).ToList();
        var unresolved = detail.Links.Where(l => l.Target is null).ToList();

        builder.AppendLine(CultureInfo.InvariantCulture, $"Links to ({resolved.Count}):");
        foreach (var link in resolved)
        {
            var heading = link.Heading is null ? string.Empty : $" (section \"{OneLine(link.Heading, 80)}\")";
            builder.Append("- ").Append(Ref(link.Target!)).AppendLine(heading);
        }

        if (unresolved.Count > 0)
        {
            builder.AppendLine("Links to notes that do not exist yet: " + string.Join(", ", unresolved.Select(l => $"[[{OneLine(l.Text, 80)}]]")));
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"Backlinks - notes linking here ({detail.Backlinks.Count}):");
        foreach (var backlink in detail.Backlinks)
        {
            builder.Append("- ").AppendLine(Ref(backlink));
        }
    }

    private static string Ref(VaultNoteRef note)
    {
        var tags = note.Tags.Count > 0 ? " " + string.Join(' ', note.Tags.Take(8).Select(t => "#" + OneLine(t, 40))) : string.Empty;
        return $"\"{OneLine(note.Title, 120)}\" ({OneLine(note.Path, 200)}){tags} - Open: {note.ObsidianUri}";
    }

    private static string RelationLabel(VaultRelation relation) => relation switch
    {
        VaultRelation.LinksTo => "Links to",
        VaultRelation.LinkedFrom => "Linked from",
        VaultRelation.SharesLinks => "Shares links with (not linked directly)",
        _ => "Similar content"
    };

    private static string StrengthLabel(VaultRelatedNote note) => note.Relation switch
    {
        VaultRelation.Similar => string.Create(CultureInfo.InvariantCulture, $" - similarity {note.Strength:P0}"),
        VaultRelation.SharesLinks => string.Create(CultureInfo.InvariantCulture, $" - {note.Strength:0} shared neighbour(s)"),
        _ => string.Empty
    };

    private static void Fence(StringBuilder builder, string content)
    {
        builder.AppendLine("<<<");
        builder.AppendLine(Defang(content.Trim()));
        builder.AppendLine(">>>");
    }

    /// <summary>
    /// Breaks the fence markers so note text cannot close the data fence and continue as "instructions".
    /// </summary>
    private static string Defang(string value)
        => value.Replace("<<<", "< < <", StringComparison.Ordinal).Replace(">>>", "> > >", StringComparison.Ordinal);

    private static string Cap(string value, int max) => value.Length <= max ? value : value[..max] + "…";

    private static string Finish(StringBuilder builder) => builder.ToString().TrimEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
}
