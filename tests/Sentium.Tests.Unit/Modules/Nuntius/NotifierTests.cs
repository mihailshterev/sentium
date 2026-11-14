using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Identity.Contracts;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Features.Inbox;
using Sentium.Nuntius.Features.Publishing;
using Sentium.Nuntius.Features.Streaming;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Nuntius;

public sealed class NotifierTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Carol = Guid.NewGuid();

    private static readonly NotificationMessage IncidentMessage =
        new(NotificationCategories.SystemIncident, NotificationSeverity.Critical, "Sandbox is down", DedupeKey: "system.incident:Sandbox:opened");

    private readonly IUserDirectory _directory = Substitute.For<IUserDirectory>();
    private readonly InMemoryNotificationStore _store = new();
    private readonly NotificationFeed _feed = new();
    private readonly List<INotificationChannel> _channels = [];

    public NotifierTests()
    {
        _directory.GetSovereignIdsAsync(Arg.Any<CancellationToken>()).Returns([Alice, Bob]);
        _directory.GetHouseholdIdsAsync(Arg.Any<CancellationToken>()).Returns([Alice, Bob, Carol]);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Notifier CreateSut() => new(_directory, _store, _feed, _channels, NullLogger<Notifier>.Instance);

    [Fact]
    public async Task Sovereigns_GetOneNotificationEach_PushedToTheirOwnFeed()
    {
        using var aliceFeed = _feed.Subscribe(Alice);
        using var carolFeed = _feed.Subscribe(Carol);

        await CreateSut().NotifyAsync(NotificationRecipient.Sovereigns, IncidentMessage, Ct);

        _store.Saved.Select(p => p.UserId).Should().BeEquivalentTo([Alice, Bob]);
        aliceFeed.Reader.TryRead(out var frame).Should().BeTrue();
        frame!.Type.Should().Be(NotificationFeedEventTypes.Notification);
        frame.Data.Should().BeOfType<NotificationDto>().Which.Title.Should().Be("Sandbox is down");
        carolFeed.Reader.TryRead(out _).Should().BeFalse("Carol is not a Sovereign");
    }

    [Fact]
    public async Task Household_ReachesEveryMember()
    {
        await CreateSut().NotifyAsync(NotificationRecipient.Household, IncidentMessage, Ct);

        _store.Saved.Select(p => p.UserId).Should().BeEquivalentTo([Alice, Bob, Carol]);
    }

    [Fact]
    public async Task SingleUser_IsNotLookedUpInTheDirectory()
    {
        await CreateSut().NotifyAsync(NotificationRecipient.User(Carol), IncidentMessage, Ct);

        _store.Saved.Should().ContainSingle().Which.UserId.Should().Be(Carol);
        await _directory.DidNotReceive().GetSovereignIdsAsync(Arg.Any<CancellationToken>());
        await _directory.DidNotReceive().GetHouseholdIdsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailingChannel_DoesNotFailThePublishOrTheOtherChannels()
    {
        var broken = Substitute.For<INotificationChannel>();
        broken.DeliverAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new HttpRequestException("ntfy is down")));
        var working = Substitute.For<INotificationChannel>();
        _channels.AddRange([broken, working]);
        using var aliceFeed = _feed.Subscribe(Alice);

        var publish = () => CreateSut().NotifyAsync(NotificationRecipient.Sovereigns, IncidentMessage, Ct);

        await publish.Should().NotThrowAsync();
        _store.Saved.Should().HaveCount(2, "the Inbox write stands");
        aliceFeed.Reader.TryRead(out _).Should().BeTrue();
        await working.Received(1).DeliverAsync(Alice, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
        await working.Received(1).DeliverAsync(Bob, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailingInboxWrite_IsSwallowed_AndNothingIsDelivered()
    {
        _store.Failure = new InvalidOperationException("database unavailable");
        var channel = Substitute.For<INotificationChannel>();
        _channels.Add(channel);

        var publish = () => CreateSut().NotifyAsync(NotificationRecipient.User(Alice), IncidentMessage, Ct);

        await publish.Should().NotThrowAsync();
        await channel.DidNotReceive().DeliverAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailedInboxWrite_ForOneRecipient_DoesNotStopTheOthers()
    {
        _store.FailingUsers.Add(Alice);
        var channel = Substitute.For<INotificationChannel>();
        _channels.Add(channel);
        using var bobFeed = _feed.Subscribe(Bob);

        var publish = () => CreateSut().NotifyAsync(NotificationRecipient.Sovereigns, IncidentMessage, Ct);

        await publish.Should().NotThrowAsync();
        _store.Saved.Select(s => s.UserId).Should().Equal(Bob);
        bobFeed.Reader.TryRead(out _).Should().BeTrue();
        await channel.Received(1).DeliverAsync(Bob, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
        await channel.DidNotReceive().DeliverAsync(Alice, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailingUserLookup_IsSwallowed()
    {
        _directory.GetSovereignIdsAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Guid>>(_ => throw new InvalidOperationException("identity unavailable"));

        var publish = () => CreateSut().NotifyAsync(NotificationRecipient.Sovereigns, IncidentMessage, Ct);

        await publish.Should().NotThrowAsync();
        _store.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Feed_KeepsUsersApart()
    {
        using var aliceFeed = _feed.Subscribe(Alice);
        using var bobFeed = _feed.Subscribe(Bob);

        await CreateSut().NotifyAsync(NotificationRecipient.User(Bob), IncidentMessage, Ct);

        aliceFeed.Reader.TryRead(out _).Should().BeFalse();
        bobFeed.Reader.TryRead(out _).Should().BeTrue();
    }

    private sealed class InMemoryNotificationStore : INotificationStore
    {
        public List<(Guid UserId, NotificationDto Notification)> Saved { get; } = [];

        public Exception? Failure { get; set; }

        public HashSet<Guid> FailingUsers { get; } = [];

        public Task<NotificationDto> SaveAsync(Guid userId, NotificationMessage message, CancellationToken ct)
        {
            if (Failure is not null || FailingUsers.Contains(userId))
            {
                throw Failure ?? new InvalidOperationException("write failed");
            }

            var now = DateTimeOffset.UtcNow;
            var notification = new NotificationDto(
                Guid.NewGuid(), message.Category, message.Severity, message.Title, message.Body, message.Link, 1, now, now, null);

            Saved.Add((userId, notification));
            return Task.FromResult(notification);
        }
    }
}
