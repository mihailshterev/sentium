using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.E2ESeed;
using Sentium.Locus.Data;
using Sentium.Praxis.Data;
using Sentium.Tempus.Data;
using Xunit;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// The Playwright suite relies on the baseline data that Sentium.E2ESeed seeds when the host starts in the Testing
/// environment. This boots the host that way and checks each module's seed landed.
/// </summary>
public sealed class E2ESeedingTests(TestingEnvironmentWebApplicationFactory factory) : IClassFixture<TestingEnvironmentWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TestingEnvironment_SeedsEveryModulesBaselineData()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();

        var praxis = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        var tempus = scope.ServiceProvider.GetRequiredService<TempusDbContext>();
        var locus = scope.ServiceProvider.GetRequiredService<LocusDbContext>();

        var agents = await praxis.Database
            .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM praxis."Agents" WHERE "Name" = 'e2e-baseline-agent'""")
            .SingleAsync(Ct);
        var tasks = await tempus.Database
            .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM tempus."TimelineTasks" WHERE "Title" = {TempusE2EDataSeeder.TodayTaskTitle}""")
            .SingleAsync(Ct);
        var assets = await locus.Database
            .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM locus."Assets" WHERE "DisplayName" = {LocusE2EDataSeeder.AssetName}""")
            .SingleAsync(Ct);

        agents.Should().Be(1);
        tasks.Should().Be(1);
        assets.Should().Be(1);
    }
}
