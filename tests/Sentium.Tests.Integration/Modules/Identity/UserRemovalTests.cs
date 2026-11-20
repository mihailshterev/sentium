using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Codex.Data;
using Sentium.Core.Constants;
using Sentium.Identity.Features.Users;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;
using Sentium.Registry.Contracts;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Identity;

/// <summary>
/// Deleting an account removes what the other modules hold for it, so nothing of the user lingers or keeps acting.
/// </summary>
public sealed class UserRemovalTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeletingAUser_RemovesTheirDataInOtherModules()
    {
        var userId = await factory.CreateUserAsync();
        var bystander = await factory.CreateUserAsync();
        await SeedAsync(userId);
        await SeedAsync(bystander);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var (succeeded, errors) = await scope.ServiceProvider.GetRequiredService<IUserService>().DeleteUserAsync(Guid.NewGuid(), userId, Ct);
            Assert.True(succeeded, string.Join(", ", errors));
        }

        Assert.Equal((0, 0, false), await CountAsync(userId));
        Assert.Equal((1, 1, true), await CountAsync(bystander));
    }

    private async Task SeedAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var praxis = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        praxis.Conversations.Add(new Conversation { Id = Guid.NewGuid(), UserId = userId, Title = "private", Model = "m", CreatedAt = DateTime.UtcNow });
        await praxis.SaveChangesAsync(Ct);

        var embedding = new float[CodexDbContext.EmbeddingDimensions];
        embedding[0] = 1f;
        var chunk = new DocumentChunk { Content = "private", Source = "notes.txt", Scope = KnowledgeScope.User, UserId = userId };
        await scope.ServiceProvider.GetRequiredService<IVectorRepository>().AddRangeAsync(KnowledgeCollections.KnowledgeBase, [new EmbeddedChunk(chunk, embedding)], Ct);

        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .UpdateAsync(SettingsKeys.Companion, userId, JsonSerializer.SerializeToElement(new { isVoiceEnabled = false }), "test", Ct);
    }

    private async Task<(int Conversations, int Chunks, bool HasSettings)> CountAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var conversations = await scope.ServiceProvider.GetRequiredService<PraxisDbContext>().Conversations.CountAsync(c => c.UserId == userId, Ct);
        var chunks = await scope.ServiceProvider.GetRequiredService<CodexDbContext>().KnowledgeChunks.CountAsync(c => c.UserId == userId, Ct);

        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetValueAsync<CompanionValue>(SettingsKeys.Companion, userId, Ct);

        return (conversations, chunks, settings?.IsVoiceEnabled == false);
    }

    private sealed record CompanionValue(bool IsVoiceEnabled);
}
