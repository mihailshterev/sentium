using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Agents.Approvals;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// A paused run's approval lives in the database, so a decision still resumes it after a restart (a new scope here).
/// </summary>
public sealed class PendingApprovalStoreTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OnlyTheOwner_TakesAnApproval_AndOnlyOnce()
    {
        var owner = Guid.NewGuid();
        var requestId = $"req-{Guid.NewGuid():N}";
        await InScopeAsync(store => store.AddAsync(requestId, Approval(requestId, owner), Ct));

        Assert.Null(await InScopeAsync(store => store.TakeAsync(requestId, Guid.NewGuid(), Ct)));

        var taken = await InScopeAsync(store => store.TakeAsync(requestId, owner, Ct));
        Assert.NotNull(taken);
        Assert.Equal(requestId, taken.ApprovalRequest.RequestId);
        Assert.Equal("execute_code_sandbox", Assert.IsType<FunctionCallContent>(taken.ApprovalRequest.ToolCall).Name);

        Assert.Null(await InScopeAsync(store => store.TakeAsync(requestId, owner, Ct)));
    }

    [Fact]
    public async Task TheRetentionSweep_DeletesExpiredApprovals()
    {
        var owner = Guid.NewGuid();
        var requestId = $"req-{Guid.NewGuid():N}";
        await InScopeAsync(store => store.AddAsync(requestId, Approval(requestId, owner), Ct));

        await InScopeAsync(store => ((PendingApprovalStore)store).SweepAsync(DateTimeOffset.UtcNow + PendingApprovalStore.Ttl + TimeSpan.FromMinutes(1), Ct));

        Assert.Null(await InScopeAsync(store => store.TakeAsync(requestId, owner, Ct)));
    }

    private async Task<T> InScopeAsync<T>(Func<IPendingApprovalStore, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return await action(scope.ServiceProvider.GetRequiredService<IPendingApprovalStore>());
    }

    private async Task InScopeAsync(Func<IPendingApprovalStore, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        await action(scope.ServiceProvider.GetRequiredService<IPendingApprovalStore>());
    }

    private static PendingApproval Approval(string requestId, Guid owner) => new(
        Session: JsonDocument.Parse("""{"stateBag":{}}""").RootElement.Clone(),
        ApprovalRequest: new ToolApprovalRequestContent(requestId, new FunctionCallContent("call-1", "execute_code_sandbox")),
        ConversationId: null,
        Model: "test-model",
        UserId: owner);
}
