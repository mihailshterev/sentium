using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Extensions;
using Sentium.Locus.Data;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Locus;

/// <summary>
/// Pins the SQL Npgsql generates for the search building blocks (no database needed: ToQueryString never
/// opens a connection).
/// </summary>
public sealed class LocusQueryTranslationTests : IDisposable
{
    private readonly LocusDbContext _db = CreateContext();

    private static LocusDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LocusDbContext>();
        options.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, LocusDbContext.Schema);
        return new LocusDbContext(options.Options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void SearchTerms_TranslateToEscapedLike()
    {
        var tree = new LocationTree([]);

        var sql = AssetSearch.ApplyTerms(_db.Assets, ["100%"], tree).ToQueryString();

        sql.Should().Contain("LIKE").And.Contain("ESCAPE");
        sql.Should().Contain("\"SearchText\"");
    }

    [Fact]
    public void SearchTerms_MatchingALocation_AlsoMatchAssetsInThatSubtree()
    {
        var garage = Guid.NewGuid();
        var tree = new LocationTree([new LocationNode(garage, null, "Garage")]);

        var sql = AssetSearch.ApplyTerms(_db.Assets, ["garage"], tree).ToQueryString();

        sql.Should().Contain("\"LocationId\" = ANY");
    }

    [Fact]
    public void TagFilter_TranslatesToArrayContainment()
    {
        const string tag = "tools";

        var sql = _db.Assets.Where(a => a.Tags.Contains(tag)).ToQueryString();

        sql.Should().Contain("a.\"Tags\" @>", "array containment can use the GIN index");
    }
}
