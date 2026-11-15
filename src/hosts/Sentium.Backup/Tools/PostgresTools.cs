using System.Globalization;

namespace Sentium.Backup.Tools;

/// <summary>
/// The PostgreSQL client tools from the image, which match the server's major version. Connection details come
/// from the libpq environment variables; <c>PGDATABASE</c> names the application database.
/// </summary>
internal static class PostgresTools
{
    private const string MaintenanceDatabase = "postgres";
    private const string DefaultDatabase = "sentiumdb";

    public static string DatabaseName => Environment.GetEnvironmentVariable("PGDATABASE") is { Length: > 0 } name ? name : DefaultDatabase;

    /// <summary>
    /// Custom format is compressed and is the only one <c>pg_restore --list</c> can check. The dump runs in a
    /// single snapshot, so it is consistent while Sentium is in use.
    /// </summary>
    public static Task DumpAsync(string outputPath, CancellationToken ct)
        => ProcessRunner.RunAsync("pg_dump", ["--format=custom", "--dbname", DatabaseName, "--file", outputPath], ct);

    /// <summary>
    /// Reads the dump's table of contents; a truncated or corrupt dump fails here.
    /// </summary>
    public static async Task<int> ListEntriesAsync(string dumpPath, CancellationToken ct)
    {
        var listing = await ProcessRunner.RunAsync("pg_restore", ["--list", dumpPath], ct);
        var entries = listing.Split('\n').Count(line => line.Length > 0 && !line.StartsWith(';'));
        if (entries == 0)
        {
            throw new BackupFailedException("The database dump lists no entries.");
        }

        return entries;
    }

    /// <summary>
    /// Restores in one transaction, so a failure leaves the database as empty as it was.
    /// </summary>
    public static Task RestoreAsync(string dumpPath, CancellationToken ct)
        => ProcessRunner.RunAsync(
            "pg_restore",
            ["--exit-on-error", "--single-transaction", "--no-owner", "--dbname", DatabaseName, dumpPath],
            ct);

    public static async Task<int> CountUserTablesAsync(CancellationToken ct)
    {
        var output = await ProcessRunner.RunAsync(
            "psql",
            ["--no-psqlrc", "--tuples-only", "--no-align", "--dbname", DatabaseName, "--command",
             "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema')"],
            ct);

        return int.Parse(output.Trim(), CultureInfo.InvariantCulture);
    }

    public static Task RecreateDatabaseAsync(CancellationToken ct)
    {
        var quoted = "\"" + DatabaseName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return ProcessRunner.RunAsync(
            "psql",
            ["--no-psqlrc", "--set", "ON_ERROR_STOP=1", "--dbname", MaintenanceDatabase,
             "--command", $"DROP DATABASE IF EXISTS {quoted} WITH (FORCE)",
             "--command", $"CREATE DATABASE {quoted}"],
            ct);
    }
}
