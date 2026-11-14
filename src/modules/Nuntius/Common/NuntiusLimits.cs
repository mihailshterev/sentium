namespace Sentium.Nuntius.Common;

internal static class NuntiusLimits
{
    public const int CategoryMax = 64;
    public const int TitleMax = 200;
    public const int BodyMax = 2_000;
    public const int LinkMax = 512;
    public const int DedupeKeyMax = 256;

    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public const int DefaultRetentionDays = 90;

    public static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    public static string? TruncateOrNull(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), max);
}
