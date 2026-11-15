namespace Sentium.AppHost.Config;

/// <summary>
/// The backup sidecar, built from its Dockerfile so <c>pg_dump</c> matches the PostgreSQL server and <c>age</c>
/// is available, exactly as under Compose.
/// </summary>
internal static class BackupConfig
{
    /// <summary>
    /// Set to <c>false</c> (e.g. <c>Backup__Enabled=false</c> or user secrets) to run without the sidecar; the
    /// System page then says backups are not set up.
    /// </summary>
    public const string EnabledKey = "Backup:Enabled";

    /// <summary>
    /// Host directory for the archives. Defaults to <c>backups/</c> at the repository root (git- and docker-ignored).
    /// </summary>
    public const string DestinationKey = "Backup:Destination";

    public const string ContextPath = "../../..";
    public const string DockerfilePath = "src/hosts/Sentium.Backup/Dockerfile";
    public const string DefaultDestination = "../../../backups";
    public const string DestinationPath = "/backups";
    public const string MarkerFileName = ".sentium-backups";
    public const int ContainerPort = 8080;

    /// <summary>
    /// Under Aspire, Sentium.Web keeps its data-protection keys in the developer's profile, not in a volume. Moving
    /// them would invalidate existing dev sessions and MCP secrets, so the sidecar gets this empty volume and dev
    /// backups carry no keys.
    /// </summary>
    public const string KeysVolume = "sentium-dev-backup-keys";
    public const string KeysPath = "/keys";
}
