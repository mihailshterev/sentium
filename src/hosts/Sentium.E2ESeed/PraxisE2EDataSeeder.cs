using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Testing;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;

namespace Sentium.E2ESeed;

public sealed class PraxisE2EDataSeeder : IE2ESeeder
{
    public Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);

        return SeedAsync(services.GetRequiredService<PraxisDbContext>(), context.UserId);
    }

    private static async Task SeedAsync(PraxisDbContext db, Guid testUserId)
    {
        var now = DateTime.UtcNow;

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            Name = "e2e-baseline-agent",
            Description = "Baseline agent for E2E tests",
            Model = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Agents.Add(agent);

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            Name = "e2e-baseline-workflow",
            Description = "Baseline workflow for E2E tests",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Workflows.Add(workflow);
        db.WorkflowAgents.Add(new WorkflowAgent { WorkflowId = workflow.Id, AgentId = agent.Id, Order = 0 });

        db.Workspaces.Add(new Workspace
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            Name = "e2e-baseline-workspace",
            Description = "Baseline workspace for E2E tests",
            CreatedAt = now,
            UpdatedAt = now,
        });

        db.Conversations.Add(new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            Title = "e2e-baseline-conversation",
            Model = string.Empty,
            CreatedAt = now,
        });

        for (var i = 1; i <= 3; i++)
        {
            db.AgentLearnings.Add(new AgentLearning
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                AgentName = "e2e-baseline-agent",
                Content = $"E2E baseline learning {i}: observed pattern during automated test",
                Tags = "e2e,baseline",
                CapturedAt = DateTimeOffset.UtcNow,
                IsIngested = false,
            });
        }

        await db.SaveChangesAsync();
    }
}
