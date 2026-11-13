using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Mcp.Features.Servers;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Assistant;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Titles;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Tests.Unit.Modules.Praxis.Conversations;

public sealed class TitleGeneratorTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly TitleGenerator _generator;

    public TitleGeneratorTests()
    {
        _generator = new TitleGenerator(_chatClient, NullLogger<TitleGenerator>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSanitizedModelTitle()
    {
        RespondWith("\"Sorting a Python List.\"\nSome extra explanation");

        var title = await _generator.GenerateAsync("how do I sort a list in python?", TestContext.Current.CancellationToken);

        title.Should().Be("Sorting a Python List");
    }

    [Fact]
    public async Task GenerateAsync_SendsPromptWithoutContextTags()
    {
        IEnumerable<ChatMessage>? sent = null;
        _chatClient.GetResponseAsync(Arg.Do<IEnumerable<ChatMessage>>(m => sent = m.ToList()), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Quarterly report summary")));

        await _generator.GenerateAsync("[Workspace: Finance | ID: 123] summarize the report", TestContext.Current.CancellationToken);

        sent.Should().NotBeNull();
        sent!.Last().Text.Should().Be("summarize the report");
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToText_WhenModelFails()
    {
        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        var title = await _generator.GenerateAsync("explain kubernetes networking", TestContext.Current.CancellationToken);

        title.Should().Be("explain kubernetes networking");
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToText_WhenModelReturnsNothingUsable()
    {
        RespondWith("  \"\"  ");

        var title = await _generator.GenerateAsync("plan a trip to Lisbon", TestContext.Current.CancellationToken);

        title.Should().Be("plan a trip to Lisbon");
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToText_WhenCancelled()
    {
        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var title = await _generator.GenerateAsync("draft a cover letter", TestContext.Current.CancellationToken);

        title.Should().Be("draft a cover letter");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNull_WithoutCallingModel_WhenTextIsOnlyContextTags()
    {
        var title = await _generator.GenerateAsync("[File: data.csv | ID: 42]  ", TestContext.Current.CancellationToken);

        title.Should().BeNull();
        await _chatClient.DidNotReceive().GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Title: Weekend Hiking Plans", "Weekend Hiking Plans")]
    [InlineData("**Title:** Weekend Hiking Plans", "Weekend Hiking Plans")]
    [InlineData("# Weekend   Hiking\tPlans!", "Weekend Hiking Plans")]
    [InlineData("\n\n  “Weekend Hiking Plans”  \n", "Weekend Hiking Plans")]
    [InlineData("`Weekend Hiking Plans`...", "Weekend Hiking Plans")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Sanitize_CleansModelOutput(string? raw, string? expected)
        => TitleGenerator.Sanitize(raw).Should().Be(expected);

    [Fact]
    public void Sanitize_ClampsLongTitles_OnAWordBoundary()
    {
        var raw = string.Join(' ', Enumerable.Repeat("conversation", 10));

        var title = TitleGenerator.Sanitize(raw);

        title.Should().NotBeNull();
        title!.Length.Should().BeLessThanOrEqualTo(TitleGenerator.MaxTitleLength);
        title.Should().NotEndWith(" ").And.EndWith("conversation");
    }

    [Fact]
    public void FromText_ShortensLongText_WithEllipsis()
    {
        var title = TitleGenerator.FromText("[Workspace: Ops | ID: 7] Please review the deployment pipeline configuration and suggest improvements");

        title.Should().StartWith("Please review the deployment pipeline").And.EndWith("…");
        title!.Length.Should().BeLessThanOrEqualTo(TitleGenerator.MaxFallbackLength + 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("[Workspace: Ops | ID: 7]")]
    public void FromText_ReturnsNull_WhenNothingToTitle(string? text)
        => TitleGenerator.FromText(text).Should().BeNull();

    [Fact]
    public void UpdateTitleValidator_RequiresNonBlankTitleWithinLimit()
    {
        var validator = new UpdateTitleRequestValidator();

        validator.Validate(new UpdateTitleRequest("Trip planning")).IsValid.Should().BeTrue();
        validator.Validate(new UpdateTitleRequest("   ")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateTitleRequest(new string('x', ConversationTitles.MaxLength + 1))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateConversationValidator_AllowsMissingTitle()
    {
        var validator = new CreateConversationRequestValidator();

        validator.Validate(new CreateConversationRequest(null, "qwen3")).IsValid.Should().BeTrue();
        validator.Validate(new CreateConversationRequest("Named", "qwen3")).IsValid.Should().BeTrue();
        validator.Validate(new CreateConversationRequest(null, "")).IsValid.Should().BeFalse();
        validator.Validate(new CreateConversationRequest(new string('x', ConversationTitles.MaxLength + 1), "qwen3")).IsValid.Should().BeFalse();
    }

    private void RespondWith(string text)
        => _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
}
