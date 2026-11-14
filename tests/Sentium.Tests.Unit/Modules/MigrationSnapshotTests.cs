using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sentium.Codex.Data;
using Sentium.Identity.Data;
using Sentium.Locus.Data;
using Sentium.Mcp.Data;
using Sentium.Nuntius.Data;
using Sentium.Praxis.Data;
using Sentium.Registry.Data;
using Sentium.Sentinel.Data;
using Sentium.Tempus.Data;
using Xunit;

namespace Sentium.Tests.Unit.Modules;

/// <summary>
/// Every module's EF model must match its latest migration snapshot, so a mapping change that forgot
/// <c>dotnet ef migrations add</c> fails here instead of at startup. Uses the same design-time factories
/// as the EF tooling; no database connection is opened.
/// </summary>
public sealed class MigrationSnapshotTests
{
    private static readonly Dictionary<string, Func<DbContext>> Contexts = new()
    {
        ["Identity"] = () => new IdentityDbContextFactory().CreateDbContext([]),
        ["Registry"] = () => new RegistryDbContextFactory().CreateDbContext([]),
        ["Nuntius"] = () => new NuntiusDbContextFactory().CreateDbContext([]),
        ["Sentinel"] = () => new SentinelDbContextFactory().CreateDbContext([]),
        ["Codex"] = () => new CodexDbContextFactory().CreateDbContext([]),
        ["Locus"] = () => new LocusDbContextFactory().CreateDbContext([]),
        ["Tempus"] = () => new TempusDbContextFactory().CreateDbContext([]),
        ["Praxis"] = () => new PraxisDbContextFactory().CreateDbContext([]),
        ["Mcp"] = () => new McpDbContextFactory().CreateDbContext([]),
    };

    public static TheoryData<string> ModuleNames() => [.. Contexts.Keys];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Model_HasNoChangesMissingAMigration(string module)
    {
        using var db = Contexts[module]();

        db.Database.HasPendingModelChanges().Should().BeFalse(
            $"the {module} model changed without a migration; run `dotnet ef migrations add <Name> " +
            $"--project src/modules/{module} --startup-project src/hosts/Sentium.Web " +
            $"--context {module}DbContext --output-dir Data/Migrations`");
    }
}
