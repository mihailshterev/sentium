using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill for inspecting and transforming CSV data. Complements the json-analyzer skill.
/// </summary>
internal sealed class CsvAnalyzerSkill : AgentClassSkill<CsvAnalyzerSkill>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "csv-analyzer",
        "Parse, validate, and summarize CSV data: row/column counts, inferred column types, and conversion to JSON. Use when the user provides comma-separated data or asks about a CSV file's shape or contents.",
        """
        Use this skill when the user provides CSV (comma-separated) data.

        1. Use the summarize script to report row count, column names, and the inferred type of each column.
        2. Use the validate script to detect structural problems such as ragged rows (wrong number of fields).
        3. Use the to-json script to convert the CSV into an array of objects keyed by the header row.
        4. Assume the first row is a header unless the user says otherwise.
        5. Quoted fields (with embedded commas, quotes, or newlines) are handled - report the parsed shape, not the raw text.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "csv-analyzer",
        "Parse, validate, and summarize CSV data: row/column counts, inferred column types, and conversion to JSON. Use when the user provides comma-separated data or asks about a CSV file's shape or contents.");

    protected override string Instructions => """
        Use this skill when the user provides CSV (comma-separated) data.

        1. Use the summarize script to report row count, column names, and the inferred type of each column.
        2. Use the validate script to detect structural problems such as ragged rows (wrong number of fields).
        3. Use the to-json script to convert the CSV into an array of objects keyed by the header row.
        4. Assume the first row is a header unless the user says otherwise.
        5. Quoted fields (with embedded commas, quotes, or newlines) are handled - report the parsed shape, not the raw text.
        """;

    [AgentSkillResource("csv-tips")]
    [Description("Common CSV quoting, delimiter, and encoding pitfalls.")]
    public string CsvTips => """
        # CSV Pitfalls

        ## Quoting
        - Fields containing a comma, double-quote, or newline MUST be wrapped in double quotes.
        - A literal double-quote inside a quoted field is escaped by doubling it: "" .

        ## Delimiters & encoding
        - The delimiter is usually a comma, but some locales export with a semicolon - confirm before parsing.
        - Save as UTF-8. A leading BOM can corrupt the first header name if not stripped.

        ## Structure
        - Every row should have the same number of fields as the header (no ragged rows).
        - Keep a single header row; avoid blank lines between data rows.
        - Numbers should not carry thousands separators; dates are safest in ISO 8601 (YYYY-MM-DD).
        """;

    [AgentSkillScript("summarize")]
    [Description("Parses CSV text and returns the row count, column names, and inferred type of each column.")]
    private static string Summarize(string csv)
    {
        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            return JsonSerializer.Serialize(new SkillError("The CSV input is empty."), JsonOptions);
        }

        var header = rows[0];
        var dataRows = rows.Skip(1).ToList();

        var columns = new List<CsvColumnInfo>(header.Count);
        for (var c = 0; c < header.Count; c++)
        {
            var values = dataRows.Where(r => c < r.Count).Select(r => r[c]);
            columns.Add(new CsvColumnInfo(header[c], InferColumnType(values)));
        }

        return JsonSerializer.Serialize(new CsvSummary(dataRows.Count, header.Count, columns), JsonOptions);
    }

    [AgentSkillScript("validate")]
    [Description("Checks CSV text for structural issues such as ragged rows and reports any problems.")]
    private static string Validate(string csv)
    {
        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            return JsonSerializer.Serialize(new CsvValidationResult(false, ["The CSV input is empty."]), JsonOptions);
        }

        var expected = rows[0].Count;
        var issues = new List<string>();

        for (var i = 1; i < rows.Count; i++)
        {
            if (rows[i].Count != expected)
            {
                issues.Add($"Row {i + 1} has {rows[i].Count} field(s) but the header has {expected}.");
            }
        }

        return JsonSerializer.Serialize(new CsvValidationResult(issues.Count == 0, issues), JsonOptions);
    }

    [AgentSkillScript("to-json")]
    [Description("Converts CSV text into a JSON array of objects keyed by the header row.")]
    private static string ToJson(string csv)
    {
        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            return JsonSerializer.Serialize(new SkillError("The CSV input is empty."), JsonOptions);
        }

        var header = rows[0];
        var records = new List<Dictionary<string, string>>(rows.Count - 1);

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var record = new Dictionary<string, string>(header.Count);
            for (var c = 0; c < header.Count; c++)
            {
                record[header[c]] = c < row.Count ? row[c] : string.Empty;
            }

            records.Add(record);
        }

        return JsonSerializer.Serialize(records, JsonOptions);
    }

    private static string InferColumnType(IEnumerable<string> values)
    {
        var seen = false;
        var allInt = true;
        var allNumber = true;
        var allBool = true;
        var allDate = true;

        foreach (var raw in values)
        {
            var value = raw.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            seen = true;

            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                allInt = false;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                allNumber = false;
            }

            if (!bool.TryParse(value, out _))
            {
                allBool = false;
            }

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                allDate = false;
            }
        }

        if (!seen)
        {
            return "empty";
        }

        if (allInt)
        {
            return "integer";
        }

        if (allNumber)
        {
            return "number";
        }

        if (allBool)
        {
            return "boolean";
        }

        if (allDate)
        {
            return "date";
        }

        return "string";
    }

    /// <summary>
    /// Minimal RFC 4180-style CSV parser: comma-delimited, double-quote quoting with doubled-quote
    /// escapes, and newlines permitted inside quoted fields.
    /// </summary>
    private static List<List<string>> ParseCsv(string? csv)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(csv))
        {
            return rows;
        }

        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var ch = csv[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    current.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    current.Add(field.ToString());
                    field.Clear();
                    rows.Add(current);
                    current = [];
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            rows.Add(current);
        }

        return rows;
    }

    private sealed record CsvColumnInfo(string Name, string Type);

    private sealed record CsvSummary(int RowCount, int ColumnCount, IReadOnlyList<CsvColumnInfo> Columns);

    private sealed record CsvValidationResult(bool Valid, IReadOnlyList<string> Issues);
}
