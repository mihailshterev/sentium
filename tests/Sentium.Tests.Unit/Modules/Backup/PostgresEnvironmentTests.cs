using FluentAssertions;
using Sentium.Backup.Tools;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Backup;

public sealed class PostgresEnvironmentTests
{
    [Fact]
    public void AspireConnectionString_MapsToLibpqVariables()
    {
        var variables = PostgresEnvironment.ToLibpqVariables("Host=postgres;Port=5432;Username=postgres;Password=secret;Database=sentiumdb");

        variables.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["PGHOST"] = "postgres",
            ["PGPORT"] = "5432",
            ["PGUSER"] = "postgres",
            ["PGPASSWORD"] = "secret",
            ["PGDATABASE"] = "sentiumdb",
        });
    }

    [Fact]
    public void QuotedPassword_KeepsItsSemicolons()
    {
        var variables = PostgresEnvironment.ToLibpqVariables("Host=db;Username=u;Password=\"a;b=c\";Database=sentiumdb");

        variables["PGPASSWORD"].Should().Be("a;b=c");
        variables.Should().NotContainKey("PGPORT");
    }
}
