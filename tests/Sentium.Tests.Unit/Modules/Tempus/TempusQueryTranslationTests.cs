using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Data;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Tempus;

/// <summary>
/// The timeline is private even to a Sovereign: only system scopes skip the per-user filter. ToQueryString never opens
/// a connection and lists the filter's parameter values, so the filter's inputs can be pinned without a database.
/// </summary>
public sealed class TempusQueryTranslationTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static TempusDbContext CreateContext(bool sovereign, bool system)
    {
        var options = new DbContextOptionsBuilder<TempusDbContext>();
        options.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, TempusDbContext.Schema);
        return new TempusDbContext(options.Options, new StubUser(system ? null : UserId, sovereign, system));
    }

    [Fact]
    public void ASovereign_StillOnlySeesTheirOwnTasks()
    {
        using var db = CreateContext(sovereign: true, system: false);

        var sql = db.TimelineTasks.ToQueryString();

        sql.Should().Contain("\"UserId\" =").And.Contain(UserId.ToString());
        sql.Should().MatchRegex("IsSystemScope[^=\\n]*='False'");
    }

    [Fact]
    public void ASystemScope_SeesEveryUsersRows()
    {
        using var db = CreateContext(sovereign: false, system: true);

        db.TimelineProposals.ToQueryString().Should().MatchRegex("IsSystemScope[^=\\n]*='True'");
    }

    [Fact]
    public void TagFilters_UseTheArrayIndex()
    {
        using var db = CreateContext(sovereign: false, system: false);

        var sql = db.TimelineTasks.Where(t => t.Tags.Contains("health")).ToQueryString();

        sql.Should().Contain("\"Tags\"");
    }

    private sealed class StubUser(Guid? userId, bool sovereign, bool system) : ICurrentUser
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => userId is not null;
        public bool IsSovereign => sovereign;
        public bool IsSystem => system;
    }
}
