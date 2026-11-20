using Sentium.Backup.Tools;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class PostgresEnvironmentTests
{
    [Fact]
    public void AspireConnectionString_MapsToLibpqVariables()
    {
        var variables = PostgresEnvironment.ToLibpqVariables("Host=postgres;Port=5432;Username=postgres;Password=secret;Database=sentiumdb");

        Assert.Equivalent(new Dictionary<string, string>
        {
            ["PGHOST"] = "postgres",
            ["PGPORT"] = "5432",
            ["PGUSER"] = "postgres",
            ["PGPASSWORD"] = "secret",
            ["PGDATABASE"] = "sentiumdb",
        }, variables);
    }

    [Fact]
    public void QuotedPassword_KeepsItsSemicolons()
    {
        var variables = PostgresEnvironment.ToLibpqVariables("Host=db;Username=u;Password=\"a;b=c\";Database=sentiumdb");

        Assert.Equal("a;b=c", variables["PGPASSWORD"]);
        Assert.DoesNotContain("PGPORT", variables);
    }
}
