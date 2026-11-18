using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Sentium.Core.Text;
using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Parsing;

/// <summary>
/// Extracts what Sentium needs from an Obsidian note: frontmatter, tags, links and the text split by heading.
/// </summary>
/// <remarks>
/// Markdig does not know wikilinks and splits them across inline nodes, so it is only used to find the block
/// structure. Code blocks, frontmatter, HTML blocks, inline code and <c>%% comments %%</c> are blanked out of a copy
/// of the source (keeping offsets), and links and tags are then matched on that copy, which is how Obsidian ignores
/// them inside code.
/// </remarks>
internal static partial class MarkdownNoteParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();

    private static readonly HashSet<string> AttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".avif", ".pdf", ".mp3", ".wav", ".m4a", ".ogg",
        ".flac", ".webm", ".mp4", ".mov", ".mkv", ".canvas", ".base", ".excalidraw"
    };

    public static ParsedNote Parse(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var document = Markdown.Parse(content, Pipeline);
        var masked = content.ToCharArray();
        var frontmatter = Frontmatter.Empty;

        foreach (var block in document.Descendants<Block>())
        {
            switch (block)
            {
                case YamlFrontMatterBlock yaml:
                    frontmatter = FrontmatterReader.Read(yaml.Lines.ToString());
                    Blank(masked, yaml.Span);
                    break;
                case CodeBlock or HtmlBlock:
                    Blank(masked, block.Span);
                    break;
            }
        }

        var maskedText = BlankInline(new string(masked));
        var folder = VaultPaths.FolderOf(path);

        var links = ParseWikiLinks(maskedText)
            .Concat(ParseMarkdownLinks(maskedText, folder))
            .DistinctBy(l => (l.TargetKey, l.Heading, l.IsEmbed))
            .Take(MemoriaLimits.LinksPerNoteMax)
            .ToList();

        var tags = frontmatter.Tags
            .Concat(ParseInlineTags(maskedText))
            .Distinct(StringComparer.Ordinal)
            .Take(MemoriaLimits.TagsPerNoteMax)
            .ToList();

        var sections = BuildSections(document, content);
        var wordCount = sections.Sum(s => Words().Count(s.Text));

        return new ParsedNote(tags, frontmatter.Aliases, frontmatter.Json, links, sections, wordCount);
    }

    /// <summary>
    /// Replaces wikilinks with the text Obsidian shows for them (the alias, else the target), so embeddings see words
    /// rather than link syntax.
    /// </summary>
    public static string SimplifyLinks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var withoutComments = Comment().Replace(text, string.Empty);
        return WikiLink().Replace(withoutComments, ToDisplayText);
    }

    /// <summary>
    /// The lower-case key a link target resolves by, or <see langword="null"/> for a target that is not a note
    /// (an attachment, or empty).
    /// </summary>
    public static string? ToTargetKey(string target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var normalized = target.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(normalized);
        if (extension.Length > 0 && AttachmentExtensions.Contains(extension))
        {
            return null;
        }

        var key = VaultPaths.ToKey(normalized);
        return key.Length is 0 or > MemoriaLimits.PathMax ? null : key;
    }

    private static string ToDisplayText(Match match)
    {
        var (target, heading, alias) = SplitWikiLink(match.Groups["inner"].Value);
        if (alias is not null)
        {
            return alias;
        }

        var title = target.Length == 0 ? string.Empty : VaultPaths.TitleOf(target.Trim());
        return heading is null ? title : $"{title} > {heading}".Trim(' ', '>');
    }

    private static IEnumerable<ParsedLink> ParseWikiLinks(string text)
    {
        foreach (Match match in WikiLink().Matches(text))
        {
            var (target, heading, _) = SplitWikiLink(match.Groups["inner"].Value);
            if (ToTargetKey(target) is { } key)
            {
                yield return new ParsedLink(target.Trim(), key, Truncate(heading), match.Groups["embed"].Success);
            }
        }
    }

    private static IEnumerable<ParsedLink> ParseMarkdownLinks(string text, string sourceFolder)
    {
        foreach (Match match in MarkdownLink().Matches(text))
        {
            if (ToMarkdownLink(match, sourceFolder) is { } link)
            {
                yield return link;
            }
        }
    }

    private static ParsedLink? ToMarkdownLink(Match match, string sourceFolder)
    {
        var href = match.Groups["href"].Value.Trim('<', '>');
        if (href.Length == 0 || href.StartsWith('#') || UriScheme().IsMatch(href))
        {
            return null;
        }

        var hashIndex = href.IndexOf('#', StringComparison.Ordinal);
        var heading = hashIndex >= 0 ? Unescape(href[(hashIndex + 1)..]) : null;
        var target = Unescape(hashIndex >= 0 ? href[..hashIndex] : href);

        // Only links to notes: a .md file or an extension-less name.
        var extension = Path.GetExtension(target);
        if (extension.Length > 0 && !extension.Equals(VaultPaths.NoteExtension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (target.StartsWith("./", StringComparison.Ordinal) || target.StartsWith("../", StringComparison.Ordinal))
        {
            if (VaultPaths.Combine(sourceFolder, target) is not { } combined)
            {
                return null;
            }

            target = combined;
        }

        return ToTargetKey(target) is { } key
            ? new ParsedLink(target, key, Truncate(string.IsNullOrEmpty(heading) ? null : heading), match.Groups["embed"].Success)
            : null;
    }

    private static string Unescape(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static IEnumerable<string> ParseInlineTags(string text)
    {
        foreach (Match match in InlineTag().Matches(text))
        {
            var tag = match.Groups["tag"].Value.TrimEnd('/');

            // Obsidian requires at least one non-numeric character, so "#2024" is not a tag.
            if (tag.Any(c => !char.IsDigit(c) && c != '/') && TagNormalizer.NormalizeOne(tag) is { } normalized)
            {
                yield return normalized;
            }
        }
    }

    private static List<NoteSection> BuildSections(MarkdownDocument document, string content)
    {
        var sections = new List<NoteSection>();
        var headingStack = new List<(int Level, string Text)>();
        var current = new StringBuilder();

        foreach (var block in document)
        {
            switch (block)
            {
                case YamlFrontMatterBlock:
                    continue;

                case HeadingBlock heading:
                    FlushSection(sections, headingStack, current);

                    while (headingStack.Count > 0 && headingStack[^1].Level >= heading.Level)
                    {
                        headingStack.RemoveAt(headingStack.Count - 1);
                    }

                    headingStack.Add((heading.Level, HeadingText(content, heading)));
                    break;

                default:
                    current.AppendLine(Slice(content, block.Span)).AppendLine();
                    break;
            }
        }

        FlushSection(sections, headingStack, current);
        return sections;
    }

    private static void FlushSection(List<NoteSection> sections, List<(int Level, string Text)> headingStack, StringBuilder current)
    {
        var text = SimplifyLinks(current.ToString()).Trim();
        current.Clear();

        if (text.Length > 0 || headingStack.Count > 0)
        {
            sections.Add(new NoteSection([.. headingStack.Select(h => h.Text)], text));
        }
    }

    private static string HeadingText(string content, HeadingBlock heading)
    {
        var line = Slice(content, heading.Span).Split('\n')[0];
        var text = SimplifyLinks(line.Trim().TrimStart('#').TrimEnd('#')).Trim();
        return Truncate(text) ?? string.Empty;
    }

    private static (string Target, string? Heading, string? Alias) SplitWikiLink(string inner)
    {
        var pipe = inner.IndexOf('|', StringComparison.Ordinal);
        var alias = pipe >= 0 ? inner[(pipe + 1)..].Trim() : null;

        // Inside tables the alias pipe is escaped as "\|".
        var reference = (pipe >= 0 ? inner[..pipe] : inner).TrimEnd('\\');

        var hashIndex = reference.IndexOf('#', StringComparison.Ordinal);
        var target = hashIndex >= 0 ? reference[..hashIndex] : reference;
        var heading = hashIndex >= 0 ? reference[(hashIndex + 1)..].Trim().TrimStart('^') : null;

        return (target, string.IsNullOrEmpty(heading) ? null : heading, string.IsNullOrEmpty(alias) ? null : alias);
    }

    private static string BlankInline(string text)
    {
        var withoutComments = Comment().Replace(text, m => Blanks(m.Value));
        return InlineCode().Replace(withoutComments, m => Blanks(m.Value));
    }

    private static string Blanks(string value)
    {
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = source[i] == '\n' ? '\n' : ' ';
            }
        });
    }

    private static void Blank(char[] text, SourceSpan span)
    {
        var end = Math.Min(span.End, text.Length - 1);
        for (var i = Math.Max(span.Start, 0); i <= end; i++)
        {
            if (text[i] != '\n')
            {
                text[i] = ' ';
            }
        }
    }

    private static string Slice(string content, SourceSpan span)
    {
        var start = Math.Clamp(span.Start, 0, content.Length);
        var end = Math.Clamp(span.End + 1, start, content.Length);
        return content[start..end];
    }

    private static string? Truncate(string? value)
    {
        return value is { Length: > MemoriaLimits.HeadingMax } ? value[..MemoriaLimits.HeadingMax] : value;
    }

    [GeneratedRegex(@"(?<embed>!)?\[\[(?<inner>[^\[\]\n]+?)\]\]")]
    private static partial Regex WikiLink();

    [GeneratedRegex(@"(?<embed>!)?\[[^\]\n]*\]\((?<href><[^>\n]+>|[^)\s]+)(?:\s+""[^""\n]*"")?\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:")]
    private static partial Regex UriScheme();

    [GeneratedRegex(@"(?<=^|[\s(,])#(?<tag>[\p{L}\p{N}\p{M}_\-/]+)", RegexOptions.Multiline)]
    private static partial Regex InlineTag();

    [GeneratedRegex(@"%%.*?%%", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\S+")]
    private static partial Regex Words();
}
