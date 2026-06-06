using System.Text.RegularExpressions;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

/// <summary>
/// Cleans squad agent output before it enters the shared transcript. Removes the handoff sentinels the harness asks
/// agents to emit ("STATUS: COMPLETED" / "STATUS: NEEDS_ACTION") so the next sequential agent doesn't conclude the
/// whole task is finished and the Validator's view isn't polluted. The Validator's own STATUS line is never sanitized.
/// </summary>
public static partial class TranscriptSanitizer
{
    [GeneratedRegex(@"^[ \t>*#-]*STATUS:\s*(COMPLETED|NEEDS[_ ]ACTION|DONE|IN[_ ]PROGRESS)\b.*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex HandoffLineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLinesRegex();

    public static string StripHandoffLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var stripped = HandoffLineRegex().Replace(text, string.Empty);
        return ExtraBlankLinesRegex().Replace(stripped, "\n\n").Trim();
    }
}
