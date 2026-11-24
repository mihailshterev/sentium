using Sentium.Praxis.Features.Conversations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Conversations;

public sealed class HistoryWindowTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ShortConversation_IsShownWhole()
    {
        var messages = Messages(12);

        Assert.Same(messages, HistoryWindow.Select(messages, summarizedThrough: null));
    }

    [Fact]
    public void WithoutSummary_EverythingLoadedStaysVisible()
    {
        var messages = Messages(HistoryWindow.MaxLoadedMessages);

        Assert.Equal(HistoryWindow.MaxLoadedMessages, HistoryWindow.Select(messages, summarizedThrough: null).Count);
    }

    [Fact]
    public void Summary_HidesWhatItCovers_AndKeepsTheGap()
    {
        var messages = Messages(28);

        // The summary covers messages 0..4; 5..7 are older than the recent 20 but not summarized yet.
        var visible = HistoryWindow.Select(messages, summarizedThrough: messages[4].Timestamp);

        Assert.Equal(23, visible.Count);
        Assert.Equal("m5", visible[0].Content);
        Assert.Equal("m27", visible[^1].Content);
    }

    [Fact]
    public void UpToDateSummary_LeavesTheRecentWindow()
    {
        var messages = Messages(28);

        var visible = HistoryWindow.Select(messages, summarizedThrough: messages[7].Timestamp);

        Assert.Equal(HistoryWindow.RecentMessages, visible.Count);
        Assert.Equal("m8", visible[0].Content);
    }

    private static List<MessageResponse> Messages(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new MessageResponse(Guid.NewGuid(), i % 2 == 0 ? "User" : "Assistant", $"m{i}", Start.AddMinutes(i)))];
}
