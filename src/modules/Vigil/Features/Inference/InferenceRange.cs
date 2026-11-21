namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// A time window the inference charts can show and the bucket size that keeps each chart to a few hundred points.
/// </summary>
internal sealed record InferenceRange(string Name, int LengthMinutes, int StepMinutes)
{
    private const int Hour = 60;
    private const int Day = 24 * Hour;

    public static readonly IReadOnlyList<InferenceRange> All =
    [
        new("1h", Hour, 1),
        new("6h", 6 * Hour, 5),
        new("24h", Day, 15),
        new("7d", 7 * Day, Hour),
        new("30d", 30 * Day, 6 * Hour),
    ];

    public static InferenceRange Default => All[2];

    public static InferenceRange? Find(string? name)
    {
        return name is null ? Default : All.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
