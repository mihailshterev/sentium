using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Praxis.Features.Memories;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Personal memories: saved, recalled, corrected and forgotten one by one, private to their user (Sovereigns included).
/// </summary>
public sealed class UserMemoryTests(WordEmbeddingWebApplicationFactory factory) : IClassFixture<WordEmbeddingWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Remember_Recall_Update_Forget()
    {
        var userId = Guid.NewGuid();

        await WithServiceAsync(async memories =>
        {
            var saved = await memories.RememberAsync(userId, "I live in Plovdiv near the old town", Ct);
            Assert.False(saved.ReplacedExisting);

            var recalled = Assert.Single(await memories.RecallAsync(userId, "I live in Plovdiv near the", 5, Ct));
            Assert.Equal(saved.Memory.Id, recalled.Memory.Id);

            var updated = await memories.UpdateAsync(userId, saved.Memory.Id, "I live in Sofia near the central park", Ct);
            Assert.Equal("I live in Sofia near the central park", updated!.Content);
            Assert.Empty(await memories.RecallAsync(userId, "Plovdiv old town", 5, Ct));
            Assert.Single(await memories.RecallAsync(userId, "I live in Sofia near the central", 5, Ct));

            Assert.True(await memories.ForgetAsync(userId, saved.Memory.Id, Ct));
            Assert.Empty(await memories.ListAsync(userId, Ct));
            Assert.Empty(await memories.RecallAsync(userId, "I live in Sofia near the central", 5, Ct));
        });
    }

    [Fact]
    public async Task Remember_RestatedFact_ReplacesInsteadOfDuplicating()
    {
        var userId = Guid.NewGuid();

        await WithServiceAsync(async memories =>
        {
            var first = await memories.RememberAsync(userId, "My favourite colour is green.", Ct);
            var again = await memories.RememberAsync(userId, "my favourite colour is GREEN", Ct);

            Assert.True(again.ReplacedExisting);
            Assert.Equal(first.Memory.Id, again.Memory.Id);
            Assert.Equal("my favourite colour is GREEN", Assert.Single(await memories.ListAsync(userId, Ct)).Content);
        });
    }

    [Fact]
    public async Task Memories_ArePrivateToTheirUser()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await WithServiceAsync(async memories =>
        {
            var secret = await memories.RememberAsync(alice, "My bank is in the blue building", Ct);

            Assert.Empty(await memories.ListAsync(bob, Ct));
            Assert.Empty(await memories.RecallAsync(bob, "My bank is in the blue building", 5, Ct));
            Assert.Null(await memories.UpdateAsync(bob, secret.Memory.Id, "hijacked", Ct));
            Assert.False(await memories.ForgetAsync(bob, secret.Memory.Id, Ct));
            Assert.Single(await memories.ListAsync(alice, Ct));
        });
    }

    [Fact]
    public async Task Api_ListsOnlyTheCallersMemories_EvenForASovereign()
    {
        var member = Guid.NewGuid();
        using var memberClient = factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, member.ToString());
        using var sovereignClient = factory.CreateClient();

        var added = await memberClient.PostAsJsonAsync("/api/praxis/memories", new SaveUserMemoryRequest("I take the train to work"), Ct);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var memory = (await added.Content.ReadFromJsonAsync<UserMemoryDto>(Ct))!;

        var mine = await memberClient.GetFromJsonAsync<List<UserMemoryDto>>("/api/praxis/memories", Ct);
        Assert.Contains(mine!, m => m.Id == memory.Id);

        var theirs = await sovereignClient.GetFromJsonAsync<List<UserMemoryDto>>("/api/praxis/memories", Ct);
        Assert.DoesNotContain(theirs!, m => m.Id == memory.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await sovereignClient.DeleteAsync($"/api/praxis/memories/{memory.Id}", Ct)).StatusCode);

        var edited = await memberClient.PutAsJsonAsync($"/api/praxis/memories/{memory.Id}", new SaveUserMemoryRequest("I cycle to work"), Ct);
        Assert.Equal("I cycle to work", (await edited.Content.ReadFromJsonAsync<UserMemoryDto>(Ct))!.Content);

        Assert.Equal(HttpStatusCode.BadRequest, (await memberClient.PostAsJsonAsync("/api/praxis/memories", new SaveUserMemoryRequest(""), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await memberClient.DeleteAsync($"/api/praxis/memories/{memory.Id}", Ct)).StatusCode);
    }

    private async Task WithServiceAsync(Func<IUserMemoryService, Task> test)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await test(scope.ServiceProvider.GetRequiredService<IUserMemoryService>());
    }
}
