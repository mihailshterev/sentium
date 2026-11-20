using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Storage;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.CodeExecution;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Sandbox runs carry the code, prompt and files of the user they acted for, so members only ever see their own.
/// </summary>
public sealed class SandboxHistoryIsolationTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Members_SeeOnlyTheirOwnRuns()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceRun = await SeedRunAsync(alice);
        var bobRun = await SeedRunAsync(bob);

        var page = await ClientFor(alice).GetFromJsonAsync<PagedResponse<SandboxExecutionLogRecord>>("/api/sandbox/executions?pageSize=100", Ct);

        var actual = page!.Items.Select(r => r.JobId);
        Assert.Contains(aliceRun.JobId, actual);
        Assert.DoesNotContain(bobRun.JobId, actual);
        Assert.Equal(HttpStatusCode.NotFound, (await ClientFor(alice).GetAsync($"/api/sandbox/executions/{bobRun.JobId}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Members_CannotDownloadAnotherUsersArtifact()
    {
        var bobRun = await SeedRunAsync(Guid.NewGuid());

        var response = await ClientFor(Guid.NewGuid()).GetAsync($"/api/sandbox/artifacts/{bobRun.JobId:N}/result.csv", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private async Task<ExecutionLogEntry> SeedRunAsync(Guid userId)
    {
        var jobId = Guid.NewGuid();
        var entry = new ExecutionLogEntry
        {
            JobId = jobId,
            ExecutedAt = DateTimeOffset.UtcNow,
            AgentId = "isolation-test",
            UserId = userId,
            CorrelationId = Guid.NewGuid().ToString(),
            Language = ExecutionLanguage.Python,
            Code = "print('secret')",
            OriginalUserPrompt = "private prompt",
            Succeeded = true,
            ExitCode = 0,
            Output = "secret",
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = false,
            SentinelAuditId = Guid.NewGuid(),
            DurationMs = 1,
            Artifacts =
            [
                new ArtifactRecord
                {
                    FileName = "result.csv",
                    MimeType = "text/csv",
                    BlobUri = ObjectUri.Create("sandbox-artifacts", $"{jobId:N}/result.csv"),
                    SizeBytes = 3
                }
            ]
        };

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        db.ExecutionLogs.Add(entry);
        await db.SaveChangesAsync(Ct);

        return entry;
    }
}
