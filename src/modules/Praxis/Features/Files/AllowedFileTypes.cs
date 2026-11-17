namespace Sentium.Praxis.Features.Files;

/// <summary>
/// Defines the file extensions accepted for workspace upload and RAG ingestion: text-readable formats plus PDF and
/// DOCX, whose text is extracted by <see cref="IFileTextExtractor"/>. Other binary formats (e.g. XLSX) need a parser
/// and are excluded.
/// </summary>
internal static class AllowedFileTypes
{
    public const string Pdf = ".pdf";

    public const string Docx = ".docx";

    public const long MaxFileSizeBytes = 100 * 1024 * 1024;

    /// <summary>
    /// The database column limit of <see cref="Entities.ProjectFile.FileName"/>.
    /// </summary>
    public const int MaxFileNameLength = 512;

    /// <summary>
    /// Case-insensitive; each extension includes the leading dot (e.g., ".txt", ".json").
    /// </summary>
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
        Pdf,
        Docx,
    };

    /// <summary>
    /// A comma-separated, alphabetically sorted list for user-facing error messages.
    /// </summary>
    public static string AllowedList => string.Join(", ", Extensions.Order());

    public static bool IsAllowed(string extension) => Extensions.Contains(extension);

    /// <summary>
    /// Whether agents may create a file of this type from plain text: document formats would be stored as invalid
    /// documents.
    /// </summary>
    public static bool IsWritableAsText(string extension) => IsAllowed(extension) && !IsDocument(extension);

    public static bool IsDocument(string extension) =>
        string.Equals(extension, Pdf, StringComparison.OrdinalIgnoreCase) || string.Equals(extension, Docx, StringComparison.OrdinalIgnoreCase);
}
