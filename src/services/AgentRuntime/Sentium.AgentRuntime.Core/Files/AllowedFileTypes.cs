namespace Sentium.AgentRuntime.Core.Files;

/// <summary>
/// Defines the file extensions accepted for workspace upload and RAG ingestion.
/// </summary>
/// <remarks>
/// <para>
/// Only plain-text–readable formats are supported; binary-only formats (e.g., XLSX)
/// require a dedicated parser and are excluded.
/// </para>
/// <para>
/// Supported categories include:
/// <list type="bullet">
/// <item><description>Documents: .txt, .md, .markdown</description></item>
/// <item><description>Data formats: .json, .jsonl, .csv, .xml, .yaml, .yml, .toml, .ini</description></item>
/// <item><description>Markup: .html, .htm</description></item>
/// <item><description>Logs: .log</description></item>
/// <item><description>Source code: .py, .js, .ts, .cs, .java, .sql, .sh, .bat, .ps1</description></item>
/// <item><description>Configuration: .conf, .cfg, .env</description></item>
/// </list>
/// </para>
/// </remarks>
public static class AllowedFileTypes
{
    /// <summary>
    /// Gets the read-only set of allowed file extensions (case-insensitive).
    /// </summary>
    /// <remarks>
    /// Each extension includes the leading dot (e.g., ".txt", ".json").
    /// </remarks>
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".markdown",
        ".json",
        ".jsonl",
        ".csv",
        ".xml",
        ".yaml",
        ".yml",
        ".html",
        ".htm",
        ".log",
        ".py",
        ".js",
        ".ts",
        ".cs",
        ".java",
        ".sql",
        ".toml",
        ".ini",
        ".env",
        ".sh",
        ".bat",
        ".ps1",
        ".conf",
        ".cfg",
    };

    /// <summary>
    /// Checks whether the specified file extension is allowed for upload and ingestion.
    /// </summary>
    /// <param name="extension">The file extension to validate (e.g., ".txt", ".json").</param>
    /// <returns><c>true</c> if the extension is in the allowed list; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The comparison is case-insensitive.
    /// </remarks>
    public static bool IsAllowed(string extension) => Extensions.Contains(extension);

    /// <summary>
    /// Returns a human-readable comma-separated list of allowed extensions for error messages.
    /// </summary>
    /// <remarks>
    /// Extensions are sorted alphabetically and suitable for display in user-facing error messages.
    /// </remarks>
    public static string AllowedList => string.Join(", ", Extensions.Order());
}
