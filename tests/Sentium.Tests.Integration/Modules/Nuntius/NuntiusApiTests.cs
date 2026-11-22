using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Features.Inbox;
using Sentium.Nuntius.Features.Retention;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Nuntius;

/// <summary>
/// The Inbox end to end: publish through <see cref="INotifier"/>, read through <c>/api/nuntius</c>. Each test acts as
/// fresh Member users (see <see cref="TestAuthHandler.UserHeader"/>), so tests never see each other's Notifications.
/// </summary>
public sealed class NuntiusApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Notifications = "/api/nuntius/notifications";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private INotifier Notifier => factory.Services.GetRequiredService<INotifier>();

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static NotificationMessage Message(string title, string? dedupeKey = null, NotificationSeverity severity = NotificationSeverity.Info)
        => new(NotificationCategories.WorkflowRun, severity, title, "Body", "/orchestration", dedupeKey);

    private static async Task<List<NotificationDto>> InboxAsync(HttpClient client, bool unreadOnly = false)
        => (await client.GetFromJsonAsync<List<NotificationDto>>($"{Notifications}?unreadOnly={unreadOnly}", Ct))!;

    private static async Task<int> UnreadCountAsync(HttpClient client)
        => (await client.GetFromJsonAsync<UnreadCountDto>($"{Notifications}/unread-count", Ct))!.Count;

    [Fact]
    public async Task Inbox_ListsNewestFirst_AndCountsUnread()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);

        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("First"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Second", severity: NotificationSeverity.Warning), Ct);

        var inbox = await InboxAsync(client);
        Assert.Equal(new string[] { "Second", "First" }, inbox.Select(n => n.Title));
        Assert.Equal(NotificationSeverity.Warning, inbox[0].Severity);
        Assert.Equal("/orchestration", inbox[0].Link);
        Assert.Equal(2, await UnreadCountAsync(client));
    }

    [Fact]
    public async Task SameDedupeKey_UpdatesTheUnreadNotification_UntilItIsRead()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);

        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Probe failing", "probe:llm"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Probe still failing", "probe:llm"), Ct);

        var single = Assert.Single(await InboxAsync(client));
        Assert.Equal("Probe still failing", single.Title);
        Assert.Equal(2, single.Occurrences);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Notifications}/{single.Id}/read", null, Ct)).StatusCode);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Probe failing again", "probe:llm"), Ct);

        var inbox = await InboxAsync(client);
        // a read Notification is never revived
        Assert.Equal(2, inbox.Count);
        Assert.Equal("Probe failing again", inbox[0].Title);
        Assert.Equal(1, inbox[0].Occurrences);
    }

    [Fact]
    public async Task ConcurrentPublishes_WithOneKey_LeaveOneUnreadNotification()
    {
        var user = Guid.NewGuid();

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => Notifier.NotifyAsync(NotificationRecipient.User(user), Message($"Flap {i}", "probe:redis"), Ct)));

        var single = Assert.Single(await InboxAsync(ClientFor(user)));
        Assert.Equal(8, single.Occurrences);
    }

    [Fact]
    public async Task Users_SeeOnlyTheirOwnInbox_SovereignIncluded()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        await Notifier.NotifyAsync(NotificationRecipient.User(alice), Message("For Alice"), Ct);

        var aliceNotification = Assert.Single(await InboxAsync(ClientFor(alice)));

        var bobClient = ClientFor(bob);
        Assert.Empty(await InboxAsync(bobClient));
        Assert.Equal(0, await UnreadCountAsync(bobClient));
        Assert.Equal(HttpStatusCode.NotFound, (await bobClient.PostAsync($"{Notifications}/{aliceNotification.Id}/read", null, Ct)).StatusCode);
        await bobClient.PostAsync($"{Notifications}/read-all", null, Ct);

        var sovereign = factory.CreateClient();
        Assert.DoesNotContain(await InboxAsync(sovereign), n => n.Id == aliceNotification.Id);

        // nobody else can mark Alice's Notifications read
        Assert.Single(await InboxAsync(ClientFor(alice), unreadOnly: true));
    }

    [Fact]
    public async Task MarkAllRead_ClearsTheUnreadCount_AndKeepsTheNotifications()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("One"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Two"), Ct);

        var response = await client.PostAsync($"{Notifications}/read-all", null, Ct);

        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<UnreadCountDto>(Ct))!.Count);
        Assert.Equal(0, await UnreadCountAsync(client));
        var actual = (await InboxAsync(client));
        Assert.Equal(2, actual.Count);
        Assert.All(actual, n => Assert.True(n.ReadAt != null));
    }

    [Fact]
    public async Task RetentionSweep_DeletesNotificationsOlderThan90Days()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Old news"), Ct);

        await SweepAsync(DateTimeOffset.UtcNow.AddDays(89));
        Assert.Single(await InboxAsync(client));

        await SweepAsync(DateTimeOffset.UtcNow.AddDays(91));
        Assert.Empty(await InboxAsync(client));
    }

    [Fact]
    public async Task RealtimeStream_PushesOnlyTheSubscribersOwnNotifications()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/realtime/stream");
        using var response = await ClientFor(alice).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));

        Assert.Equal("connected", (await ReadFrameAsync(reader, timeout.Token)).GetProperty("topic").GetString());

        await Notifier.NotifyAsync(NotificationRecipient.User(bob), Message("For Bob"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(alice), Message("For Alice"), Ct);

        var frame = await ReadFrameAsync(reader, timeout.Token);
        Assert.Equal("notifications", frame.GetProperty("topic").GetString());
        var evt = frame.GetProperty("data");
        Assert.Equal("notification", evt.GetProperty("type").GetString());
        Assert.Equal("For Alice", evt.GetProperty("data").GetProperty("title").GetString());
    }

    [Fact]
    public async Task AgentProposal_NotifiesTheTimelineOwner()
    {
        var user = Guid.NewGuid();
        var agents = factory.Services.GetRequiredService<ITimelineAgentAccess>();

        var receipt = await agents.ProposeTaskAsync(user, "TimelinePlanner",
            new TimelineTaskProposal("Renew passport", null, 2031, 4, null, null, TimelineTaskPriority.Normal, [], null, null), Ct);
        Assert.True(receipt.IsSuccess, receipt.Error);

        var notification = Assert.Single(await InboxAsync(ClientFor(user)));
        Assert.Equal(NotificationCategories.TimelineProposal, notification.Category);
        Assert.Contains("Renew passport", notification.Title, StringComparison.Ordinal);
        Assert.StartsWith("/timeline", notification.Link, StringComparison.Ordinal);
    }

    private async Task SweepAsync(DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        await scope.ServiceProvider.GetRequiredService<NotificationRetention>().SweepAsync(now, Ct);
    }

    /// <summary>
    /// The next unnamed event's data; named events (the heartbeat <c>ping</c>) are skipped.
    /// </summary>
    private static async Task<JsonElement> ReadFrameAsync(StreamReader reader, CancellationToken ct)
    {
        var named = false;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                named = true;
            }
            else if (line.Length == 0)
            {
                named = false;
            }
            else if (!named && line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return JsonSerializer.Deserialize<JsonElement>(line["data: ".Length..], WebJson);
            }
        }

        throw new EndOfStreamException("The realtime stream closed before a frame arrived.");
    }
}
