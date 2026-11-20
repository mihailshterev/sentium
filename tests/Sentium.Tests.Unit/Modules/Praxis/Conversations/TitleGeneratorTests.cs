using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

        Assert.Equal("Sorting a Python List", title);
    }

    [Fact]
    public async Task GenerateAsync_SendsPromptWithCollapsedWhitespace()
    {
        IEnumerable<ChatMessage>? sent = null;
        _chatClient.GetResponseAsync(Arg.Do<IEnumerable<ChatMessage>>(m => sent = m.ToList()), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Quarterly report summary")));

        await _generator.GenerateAsync("  summarize\n\tthe report ", TestContext.Current.CancellationToken);

        Assert.NotNull(sent);
        Assert.Equal("summarize the report", sent!.Last().Text);
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToText_WhenModelFails()
    {
        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        var title = await _generator.GenerateAsync("explain kubernetes networking", TestContext.Current.CancellationToken);

        Assert.Equal("explain kubernetes networking", title);
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToText_WhenModelReturnsNothingUsable()
    {
        RespondWith("  \"\"  ");

        var title = await _generator.GenerateAsync("plan a trip to Lisbon", TestContext.Current.CancellationToken);

        Assert.Equal("plan a trip to Lisbon", title);
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToText_WhenCancelled()
    {
        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var title = await _generator.GenerateAsync("draft a cover letter", TestContext.Current.CancellationToken);

        Assert.Equal("draft a cover letter", title);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNull_WithoutCallingModel_WhenTextIsBlank()
    {
        var title = await _generator.GenerateAsync(" \n ", TestContext.Current.CancellationToken);

        Assert.Null(title);
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
        => Assert.Equal(expected, TitleGenerator.Sanitize(raw));

    [Fact]
    public void Sanitize_ClampsLongTitles_OnAWordBoundary()
    {
        var raw = string.Join(' ', Enumerable.Repeat("conversation", 10));

        var title = TitleGenerator.Sanitize(raw);

        Assert.NotNull(title);
        Assert.True(title!.Length <= TitleGenerator.MaxTitleLength);
        Assert.False(title.EndsWith(' '));
        Assert.EndsWith("conversation", title, StringComparison.Ordinal);
    }

    [Fact]
    public void FromText_ShortensLongText_WithEllipsis()
    {
        var title = TitleGenerator.FromText("Please review the deployment pipeline configuration and suggest improvements");

        Assert.StartsWith("Please review the deployment pipeline", title, StringComparison.Ordinal);
        Assert.EndsWith("…", title, StringComparison.Ordinal);
        Assert.True(title!.Length <= TitleGenerator.MaxFallbackLength + 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("\t\n")]
    public void FromText_ReturnsNull_WhenNothingToTitle(string? text)
        => Assert.Null(TitleGenerator.FromText(text));

    [Fact]
    public void UpdateTitleValidator_RequiresNonBlankTitleWithinLimit()
    {
        var validator = new UpdateTitleRequestValidator();

        Assert.True(validator.Validate(new UpdateTitleRequest("Trip planning")).IsValid);
        Assert.False(validator.Validate(new UpdateTitleRequest("   ")).IsValid);
        Assert.False(validator.Validate(new UpdateTitleRequest(new string('x', ConversationTitles.MaxLength + 1))).IsValid);
    }

    [Fact]
    public void CreateConversationValidator_AllowsMissingTitle()
    {
        var validator = new CreateConversationRequestValidator();

        Assert.True(validator.Validate(new CreateConversationRequest(null, "qwen3")).IsValid);
        Assert.True(validator.Validate(new CreateConversationRequest("Named", "qwen3")).IsValid);
        Assert.False(validator.Validate(new CreateConversationRequest(null, "")).IsValid);
        Assert.False(validator.Validate(new CreateConversationRequest(new string('x', ConversationTitles.MaxLength + 1), "qwen3")).IsValid);
    }

    private void RespondWith(string text)
        => _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
}
