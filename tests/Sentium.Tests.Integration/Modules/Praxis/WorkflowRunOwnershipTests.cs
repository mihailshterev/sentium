using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// A run acts as the user who starts it, with no Sovereign bypass, so it could not see another user's workflow: a
/// Sovereign may see every workflow but runs only their own.
/// </summary>
public sealed class WorkflowRunOwnershipTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASovereign_CannotRunAMembersWorkflow()
    {
        var workflowId = await SeedWorkflowAsync(Guid.NewGuid());

        var response = await factory.CreateClient().PostAsJsonAsync("/api/praxis/orchestration/run-workflow", new { workflowId, scenario = "go" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheOwner_CanRunTheirWorkflow()
    {
        var member = Guid.NewGuid();
        var workflowId = await SeedWorkflowAsync(member);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, member.ToString());

        var response = await client.PostAsJsonAsync("/api/praxis/orchestration/run-workflow", new { workflowId, scenario = "go" }, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private async Task<Guid> SeedWorkflowAsync(Guid ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Name = $"wf-{Guid.NewGuid():N}",
            Description = "ownership test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(Ct);
        return workflow.Id;
    }
}
