using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Memoria.Data;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Memoria;

/// <summary>
/// A vault is private even to a Sovereign: only system scopes skip the per-user filter.
/// </summary>
public sealed class MemoriaQueryTranslationTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static MemoriaDbContext CreateContext(bool sovereign, bool system)
    {
        var options = new DbContextOptionsBuilder<MemoriaDbContext>();
        options.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, MemoriaDbContext.Schema);
        return new MemoriaDbContext(options.Options, new StubUser(system ? null : UserId, sovereign, system));
    }

    [Fact]
    public void ASovereign_StillOnlySeesTheirOwnNotes()
    {
        using var db = CreateContext(sovereign: true, system: false);

        foreach (var sql in new[] { db.Notes.ToQueryString(), db.Links.ToQueryString(), db.Vaults.ToQueryString() })
        {
            sql.Should().Contain("\"UserId\" =").And.Contain(UserId.ToString());
            sql.Should().MatchRegex("IsSystemScope[^=\\n]*='False'");
        }
    }

    [Fact]
    public void ASystemScope_SeesEveryUsersRows()
    {
        using var db = CreateContext(sovereign: false, system: true);

        db.Notes.ToQueryString().Should().MatchRegex("IsSystemScope[^=\\n]*='True'");
    }

    private sealed class StubUser(Guid? userId, bool sovereign, bool system) : ICurrentUser
    {
        public Guid? UserId => userId;
        public bool IsAuthenticated => userId is not null;
        public bool IsSovereign => sovereign;
        public bool IsSystem => system;
    }
}
