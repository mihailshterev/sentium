namespace Sentium.Backup.Archives;

/// <summary>
/// The Sentium release this sidecar was built from (<c>Version</c> in <c>Directory.Build.props</c>), which is the
/// release of the installation it runs in: every image is built from the same checkout.
/// </summary>
internal static class SentiumVersion
{
    public static Version Current { get; } = Normalize(typeof(SentiumVersion).Assembly.GetName().Version);

    public static string CurrentText => Current.ToString(3);

    public static bool TryParse(string? text, out Version version)
    {
        if (Version.TryParse(text, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }

    private static Version Normalize(Version? version)
        => version is null ? new Version(0, 0, 0) : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
}
