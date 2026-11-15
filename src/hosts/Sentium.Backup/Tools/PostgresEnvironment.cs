using System.Data.Common;
using Sentium.Core.Constants;

namespace Sentium.Backup.Tools;

/// <summary>
/// The PostgreSQL tools read their connection from the libpq environment variables. Compose sets those directly;
/// Aspire hands out an Npgsql connection string instead, which is copied onto them here so every child process
/// inherits it.
/// </summary>
internal static class PostgresEnvironment
{
    private static readonly (string[] Keys, string Variable)[] Mapping =
    [
        (["Host", "Server"], "PGHOST"),
        (["Port"], "PGPORT"),
        (["Username", "User ID", "User"], "PGUSER"),
        (["Password"], "PGPASSWORD"),
        (["Database"], "PGDATABASE")
    ];

    public static void ApplyConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ResourceNames.SentiumDb);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        foreach (var (variable, value) in ToLibpqVariables(connectionString))
        {
            Environment.SetEnvironmentVariable(variable, value);
        }
    }

    public static IReadOnlyDictionary<string, string> ToLibpqVariables(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (keys, variable) in Mapping)
        {
            var value = keys
                .Select(key => builder.TryGetValue(key, out var found) ? found?.ToString() : null)
                .FirstOrDefault(found => !string.IsNullOrEmpty(found));

            if (value is not null)
            {
                variables[variable] = value;
            }
        }

        return variables;
    }
}
