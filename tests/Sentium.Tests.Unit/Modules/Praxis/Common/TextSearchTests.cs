using NSubstitute;
using Sentium.Praxis.Common;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Common;

public sealed class TextSearchTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  trip  ", "trip")]
    public void Normalize_TrimsAndDropsBlankTerms(string? input, string? expected)
        => Assert.Equal(expected, TextSearch.Normalize(input));

    [Fact]
    public void Normalize_CutsOverlongTerms()
        => Assert.Equal(TextSearch.MaxTermLength, TextSearch.Normalize(new string('a', TextSearch.MaxTermLength + 50))!.Length);

    [Theory]
    [InlineData("trip", "%trip%")]
    [InlineData("100%", @"%100\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData(@"c:\temp", @"%c:\\temp%")]
    public void ToContainsPattern_EscapesLikeWildcards(string term, string expected)
        => Assert.Equal(expected, TextSearch.ToContainsPattern(term));

    [Fact]
    public async Task WorkflowRuns_ForwardsSearch_ToRepository()
    {
        var runs = Substitute.For<IWorkflowRunRepository>();
        runs.GetPagedAsync(1, 20, "backup", Arg.Any<CancellationToken>()).Returns((new List<WorkflowRunSummaryResponse>(), 0));
        var controller = new WorkflowsController(Substitute.For<IWorkflowService>(), runs);

        await controller.GetWorkflowRuns(1, 20, "backup", TestContext.Current.CancellationToken);

        await runs.Received(1).GetPagedAsync(1, 20, "backup", Arg.Any<CancellationToken>());
    }
}
