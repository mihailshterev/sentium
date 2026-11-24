using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Conversations;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// The conversation list after months of use: latest activity first, pinned above that, archived out of the way,
/// searchable by what was said, and long conversations opened a page of messages at a time.
/// </summary>
public sealed class ConversationListTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_FollowsActivity_Pins_AndArchives()
    {
        var (client, userId) = NewMember();
        var a = await CreateAsync(client, "A");
        var b = await CreateAsync(client, "B");
        var c = await CreateAsync(client, "C");

        await AddMessageAsync(userId, a, "the secret word is pomegranate");
        Assert.Equal(new[] { a, c, b }, await ListAsync(client));

        var found = await client.GetFromJsonAsync<PagedResponse<ConversationSummary>>("/api/praxis/conversations?search=POMEGRANATE", Ct);
        Assert.Equal(a, Assert.Single(found!.Items).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/praxis/conversations/{b}/pinned", new SetConversationPinnedRequest(true), Ct)).StatusCode);
        Assert.Equal(new[] { b, a, c }, await ListAsync(client));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/praxis/conversations/{b}/archived", new SetConversationArchivedRequest(true), Ct)).StatusCode);
        Assert.Equal(new[] { a, c }, await ListAsync(client));
        var archived = await client.GetFromJsonAsync<PagedResponse<ConversationSummary>>("/api/praxis/conversations?archived=true", Ct);
        var archivedB = Assert.Single(archived!.Items);
        Assert.Equal(b, archivedB.Id);
        Assert.False(archivedB.IsPinned);

        await AddMessageAsync(userId, b, "back again");
        Assert.Equal(new[] { b, a, c }, await ListAsync(client));
    }

    [Fact]
    public async Task LongConversation_OpensWithTheNewestMessages_AndPagesBackwards()
    {
        var (client, userId) = NewMember();
        var id = await CreateAsync(client, "Long");
        for (var i = 1; i <= 5; i++)
        {
            await AddMessageAsync(userId, id, $"message {i}");
        }

        var detail = await client.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{id}?messageLimit=2", Ct);
        Assert.Equal(new[] { "message 4", "message 5" }, detail!.Messages.Select(m => m.Content));
        Assert.True(detail.HasEarlierMessages);

        var earlier = await client.GetFromJsonAsync<MessagePageResponse>($"/api/praxis/conversations/{id}/messages?before={detail.Messages[0].Id}&limit=2", Ct);
        Assert.Equal(new[] { "message 2", "message 3" }, earlier!.Messages.Select(m => m.Content));
        Assert.True(earlier.HasEarlierMessages);

        var first = await client.GetFromJsonAsync<MessagePageResponse>($"/api/praxis/conversations/{id}/messages?before={earlier.Messages[0].Id}&limit=2", Ct);
        Assert.Equal(new[] { "message 1" }, first!.Messages.Select(m => m.Content));
        Assert.False(first.HasEarlierMessages);
    }

    [Fact]
    public async Task OtherMembers_CannotPinArchiveOrPageAConversation()
    {
        var (owner, ownerId) = NewMember();
        var (other, _) = NewMember();
        var id = await CreateAsync(owner, "Private");
        await AddMessageAsync(ownerId, id, "mine");
        var detail = await owner.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/praxis/conversations/{id}/pinned", new SetConversationPinnedRequest(true), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/praxis/conversations/{id}/archived", new SetConversationArchivedRequest(true), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/praxis/conversations/{id}/messages?before={detail!.Messages[0].Id}", Ct)).StatusCode);
    }

    private (HttpClient Client, Guid UserId) NewMember()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return (client, userId);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(title, "m"), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct))!.Id;
    }

    private static async Task<Guid[]> ListAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<ConversationSummary>>("/api/praxis/conversations", Ct);
        return [.. page!.Items.Select(c => c.Id)];
    }

    /// <summary>
    /// The way chat turns store messages: in a scope acting as the owner.
    /// </summary>
    private async Task AddMessageAsync(Guid userId, Guid conversationId, string content)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().ActAs(userId);
        await scope.ServiceProvider.GetRequiredService<IConversationService>().AddMessageAsync(conversationId, "User", content, ct: Ct);
    }
}
