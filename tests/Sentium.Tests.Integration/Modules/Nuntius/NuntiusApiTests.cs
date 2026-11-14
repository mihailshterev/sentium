using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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
        inbox.Select(n => n.Title).Should().Equal("Second", "First");
        inbox[0].Severity.Should().Be(NotificationSeverity.Warning);
        inbox[0].Link.Should().Be("/orchestration");
        (await UnreadCountAsync(client)).Should().Be(2);
    }

    [Fact]
    public async Task SameDedupeKey_UpdatesTheUnreadNotification_UntilItIsRead()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);

        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Probe failing", "probe:llm"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Probe still failing", "probe:llm"), Ct);

        var single = (await InboxAsync(client)).Should().ContainSingle().Subject;
        single.Title.Should().Be("Probe still failing");
        single.Occurrences.Should().Be(2);

        (await client.PostAsync($"{Notifications}/{single.Id}/read", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Probe failing again", "probe:llm"), Ct);

        var inbox = await InboxAsync(client);
        inbox.Should().HaveCount(2, "a read Notification is never revived");
        inbox[0].Title.Should().Be("Probe failing again");
        inbox[0].Occurrences.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentPublishes_WithOneKey_LeaveOneUnreadNotification()
    {
        var user = Guid.NewGuid();

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => Notifier.NotifyAsync(NotificationRecipient.User(user), Message($"Flap {i}", "probe:redis"), Ct)));

        var single = (await InboxAsync(ClientFor(user))).Should().ContainSingle().Subject;
        single.Occurrences.Should().Be(8);
    }

    [Fact]
    public async Task Users_SeeOnlyTheirOwnInbox_SovereignIncluded()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        await Notifier.NotifyAsync(NotificationRecipient.User(alice), Message("For Alice"), Ct);

        var aliceNotification = (await InboxAsync(ClientFor(alice))).Should().ContainSingle().Subject;

        var bobClient = ClientFor(bob);
        (await InboxAsync(bobClient)).Should().BeEmpty();
        (await UnreadCountAsync(bobClient)).Should().Be(0);
        (await bobClient.PostAsync($"{Notifications}/{aliceNotification.Id}/read", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await bobClient.PostAsync($"{Notifications}/read-all", null, Ct);

        var sovereign = factory.CreateClient();
        (await InboxAsync(sovereign)).Should().NotContain(n => n.Id == aliceNotification.Id);

        (await InboxAsync(ClientFor(alice), unreadOnly: true)).Should().ContainSingle("nobody else can mark Alice's Notifications read");
    }

    [Fact]
    public async Task MarkAllRead_ClearsTheUnreadCount_AndKeepsTheNotifications()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("One"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Two"), Ct);

        var response = await client.PostAsync($"{Notifications}/read-all", null, Ct);

        (await response.Content.ReadFromJsonAsync<UnreadCountDto>(Ct))!.Count.Should().Be(2);
        (await UnreadCountAsync(client)).Should().Be(0);
        (await InboxAsync(client)).Should().HaveCount(2).And.OnlyContain(n => n.ReadAt != null);
    }

    [Fact]
    public async Task RetentionSweep_DeletesNotificationsOlderThan90Days()
    {
        var user = Guid.NewGuid();
        var client = ClientFor(user);
        await Notifier.NotifyAsync(NotificationRecipient.User(user), Message("Old news"), Ct);

        await SweepAsync(DateTimeOffset.UtcNow.AddDays(89));
        (await InboxAsync(client)).Should().ContainSingle();

        await SweepAsync(DateTimeOffset.UtcNow.AddDays(91));
        (await InboxAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task Stream_PushesOnlyTheSubscribersOwnNotifications()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/nuntius/stream");
        using var response = await ClientFor(alice).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));

        (await ReadFrameAsync(reader, timeout.Token)).GetProperty("type").GetString().Should().Be("connected");

        await Notifier.NotifyAsync(NotificationRecipient.User(bob), Message("For Bob"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(alice), Message("For Alice"), Ct);

        var frame = await ReadFrameAsync(reader, timeout.Token);
        frame.GetProperty("type").GetString().Should().Be("notification");
        frame.GetProperty("data").GetProperty("title").GetString().Should().Be("For Alice");
    }

    [Fact]
    public async Task AgentProposal_NotifiesTheTimelineOwner()
    {
        var user = Guid.NewGuid();
        var agents = factory.Services.GetRequiredService<ITimelineAgentAccess>();

        var receipt = await agents.ProposeTaskAsync(user, "TimelinePlanner",
            new TimelineTaskProposal("Renew passport", null, 2031, 4, null, null, TimelineTaskPriority.Normal, [], null, null), Ct);
        receipt.IsSuccess.Should().BeTrue(receipt.Error);

        var notification = (await InboxAsync(ClientFor(user))).Should().ContainSingle().Subject;
        notification.Category.Should().Be(NotificationCategories.TimelineProposal);
        notification.Title.Should().Contain("Renew passport");
        notification.Link.Should().StartWith("/timeline");
    }

    private async Task SweepAsync(DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        await scope.ServiceProvider.GetRequiredService<NotificationRetention>().SweepAsync(now, Ct);
    }

    private static async Task<JsonElement> ReadFrameAsync(StreamReader reader, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return JsonSerializer.Deserialize<JsonElement>(line["data: ".Length..], WebJson);
            }
        }

        throw new EndOfStreamException("The notification stream closed before a frame arrived.");
    }
}
