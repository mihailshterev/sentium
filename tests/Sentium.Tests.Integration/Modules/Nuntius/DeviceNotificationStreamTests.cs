using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Nuntius.Contracts;
using Sentium.Tests.Integration.Common;
using Sentium.Tests.Integration.Modules.Identity;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Nuntius;

/// <summary>
/// The tray app's view of its owner's Inbox: the live stream and the unread count.
/// </summary>
public sealed class DeviceNotificationStreamTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private INotifier Notifier => factory.Services.GetRequiredService<INotifier>();

    [Fact]
    public async Task Stream_CarriesTheOwnersNotifications_AndNobodyElses()
    {
        var alice = await factory.CreateUserAsync();
        var bob = await factory.CreateUserAsync();
        var aliceDevice = await PairAsync(alice, "ALICE-STREAM");
        using var client = factory.CreateDeviceClient(aliceDevice.Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/nuntius/devices/stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        (await ReadFrameAsync(reader, timeout.Token)).GetProperty("type").GetString().Should().Be("connected");

        await Notifier.NotifyAsync(NotificationRecipient.User(bob), Message("For Bob"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(alice), Message("For Alice"), Ct);

        var frame = await ReadFrameAsync(reader, timeout.Token);
        frame.GetProperty("type").GetString().Should().Be("notification");
        var data = frame.GetProperty("data");
        data.GetProperty("title").GetString().Should().Be("For Alice");
        data.GetProperty("body").GetString().Should().Be("Body");
        data.GetProperty("link").GetString().Should().Be("/vigil");
        data.GetProperty("readAt").ValueKind.Should().Be(JsonValueKind.Null, "the tray app toasts only unread Notifications");
    }

    [Fact]
    public async Task UnreadCount_IsTheOwners()
    {
        var owner = await factory.CreateUserAsync();
        var device = await PairAsync(owner, "COUNT-PC");
        using var client = factory.CreateDeviceClient(device.Token);

        await Notifier.NotifyAsync(NotificationRecipient.User(owner), Message("One"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(owner), Message("Two"), Ct);
        await Notifier.NotifyAsync(NotificationRecipient.User(Guid.NewGuid()), Message("Someone else's"), Ct);

        var body = await client.GetFromJsonAsync<Dictionary<string, int>>("/api/nuntius/devices/unread-count", Ct);
        body.Should().BeEquivalentTo(new Dictionary<string, int> { ["count"] = 2 });
    }

    [Fact]
    public async Task RemovingTheDevice_ClosesItsOpenStream()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var device = await factory.PairAsync(portal, "CLOSED-PC", Ct);
        using var client = factory.CreateDeviceClient(device.Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/nuntius/devices/stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        await ReadFrameAsync(reader, timeout.Token);

        (await portal.DeleteAsync($"/api/identity/devices/{device.DeviceId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var closed = async () => await ReadFrameAsync(reader, timeout.Token);
        await closed.Should().ThrowAsync<EndOfStreamException>("the server ends the stream of a removed Device");
    }

    private async Task<Sentium.Identity.Features.Devices.PairDeviceResponse> PairAsync(Guid owner, string name)
    {
        using var portal = factory.CreateClientFor(owner);
        return await factory.PairAsync(portal, name, Ct);
    }

    private static NotificationMessage Message(string title)
        => new(NotificationCategories.SystemIncident, NotificationSeverity.Warning, title, "Body", "/vigil");

    private static async Task<JsonElement> ReadFrameAsync(StreamReader reader, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return JsonSerializer.Deserialize<JsonElement>(line["data: ".Length..], WebJson);
            }
        }

        throw new EndOfStreamException("The stream closed before a frame arrived.");
    }
}
