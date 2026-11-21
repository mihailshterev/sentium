namespace Sentium.Vigil.Features.Inference;

internal static class EpochMinutes
{
    private const long SecondsPerMinute = 60;

    public static long From(DateTimeOffset time)
    {
        return time.ToUnixTimeSeconds() / SecondsPerMinute;
    }

    public static DateTimeOffset ToTime(long epochMinute)
    {
        return DateTimeOffset.FromUnixTimeSeconds(epochMinute * SecondsPerMinute);
    }
}
