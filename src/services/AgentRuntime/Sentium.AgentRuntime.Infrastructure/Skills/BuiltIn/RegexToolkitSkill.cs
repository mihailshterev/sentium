using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill for building, testing, and explaining regular expressions.
/// </summary>
internal sealed class RegexToolkitSkill : AgentClassSkill<RegexToolkitSkill>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "regex-toolkit",
        "Build, test, and explain regular expressions. Use when the user needs to match, validate, or extract text with a regex, or asks for a pattern for emails, URLs, IPs, dates, and similar.",
        """
        Use this skill for anything involving regular expressions.

        1. Reach for the common-patterns resource first - it has vetted patterns for email, URL, IPv4, UUID, ISO date, phone, and slug.
        2. Use the test-pattern script to check whether a pattern matches an input and to see the matched value.
        3. Use the extract-matches script to pull every occurrence out of a larger body of text.
        4. When a pattern is invalid, report the error message verbatim and propose a corrected version.
        5. Prefer the simplest pattern that satisfies the requirement; explain what each part does.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "regex-toolkit",
        "Build, test, and explain regular expressions. Use when the user needs to match, validate, or extract text with a regex, or asks for a pattern for emails, URLs, IPs, dates, and similar.");

    protected override string Instructions => """
        Use this skill for anything involving regular expressions.

        1. Reach for the common-patterns resource first - it has vetted patterns for email, URL, IPv4, UUID, ISO date, phone, and slug.
        2. Use the test-pattern script to check whether a pattern matches an input and to see the matched value.
        3. Use the extract-matches script to pull every occurrence out of a larger body of text.
        4. When a pattern is invalid, report the error message verbatim and propose a corrected version.
        5. Prefer the simplest pattern that satisfies the requirement; explain what each part does.
        """;

    [AgentSkillResource("common-patterns")]
    [Description("Ready-to-use, vetted regular expressions for common formats.")]
    public string CommonPatterns => """
        # Common Regular Expressions

        | Purpose      | Pattern |
        |--------------|---------|
        | Email        | `^[\w.+-]+@[\w-]+\.[\w.-]+$` |
        | HTTP(S) URL  | `^https?://[^\s/$.?#].[^\s]*$` |
        | IPv4 address | `^(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)$` |
        | UUID v4      | `^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$` |
        | ISO 8601 date| `^\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z\|[+-]\d{2}:\d{2})?)?$` |
        | Slug         | `^[a-z0-9]+(?:-[a-z0-9]+)*$` |
        | E.164 phone  | `^\+[1-9]\d{1,14}$` |
        | Hex colour   | `^#(?:[0-9a-fA-F]{3}\|[0-9a-fA-F]{6})$` |

        ## Tips
        - Anchor with `^` and `$` when validating a whole value; omit them when searching within text.
        - Escape literals: `.` `+` `*` `?` `(` `)` `[` `]` `{` `}` `\` `|` `^` `$`.
        - Use non-capturing groups `(?:...)` when you do not need the captured value.
        - Prefer specific character classes over `.*` to avoid catastrophic backtracking.
        """;

    [AgentSkillScript("test-pattern")]
    [Description("Tests whether a regular expression matches an input string and returns the first match.")]
    private static string TestPattern(string pattern, string input)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.None, MatchTimeout);
            var match = regex.Match(input ?? string.Empty);
            return JsonSerializer.Serialize(
                new RegexTestResult(match.Success, match.Success ? match.Value : null, match.Success ? match.Index : -1),
                JsonOptions);
        }
        catch (RegexParseException ex)
        {
            return JsonSerializer.Serialize(new SkillError($"Invalid regular expression: {ex.Message}"), JsonOptions);
        }
        catch (RegexMatchTimeoutException)
        {
            return JsonSerializer.Serialize(new SkillError("Matching timed out - the pattern may cause catastrophic backtracking."), JsonOptions);
        }
    }

    [AgentSkillScript("extract-matches")]
    [Description("Returns every match of a regular expression found within the input text.")]
    private static string ExtractMatches(string pattern, string input)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.None, MatchTimeout);
            var matches = regex.Matches(input ?? string.Empty)
                .Select(m => m.Value)
                .ToArray();
            return JsonSerializer.Serialize(new RegexExtractResult(matches.Length, matches), JsonOptions);
        }
        catch (RegexParseException ex)
        {
            return JsonSerializer.Serialize(new SkillError($"Invalid regular expression: {ex.Message}"), JsonOptions);
        }
        catch (RegexMatchTimeoutException)
        {
            return JsonSerializer.Serialize(new SkillError("Matching timed out - the pattern may cause catastrophic backtracking."), JsonOptions);
        }
    }

    private sealed record RegexTestResult(bool Matched, string? Value, int Index);

    private sealed record RegexExtractResult(int Count, IReadOnlyList<string> Matches);
}
