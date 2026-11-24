using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Storage;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.CodeExecution;
using Sentium.Praxis.Features.Retention;
using Sentium.Tests.Integration.Common;
using Sentium.Tests.Integration.Modules.Identity;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Sandbox artifacts leave object storage with their run: when the run expires, and when no record points at them.
/// </summary>
public sealed class SandboxArtifactRetentionTests(SandboxArtifactRetentionTests.Factory factory) : IClassFixture<SandboxArtifactRetentionTests.Factory>
{
    private const string Bucket = "sandbox-artifacts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpiredRun_TakesItsArtifactsWithIt()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await RecordRunAsync(now.AddDays(-120));
        var recent = await RecordRunAsync(now.AddDays(-1));

        await using var scope = SystemScope();
        await scope.ServiceProvider.GetRequiredService<RunHistoryRetention>().SweepAsync(now, Ct);

        Assert.False(factory.ObjectStore.Contains(Bucket, ArtifactKey(expired)));
        Assert.True(factory.ObjectStore.Contains(Bucket, ArtifactKey(recent)));
    }

    [Fact]
    public async Task UnrecordedArtifacts_AreDeletedOnTheSecondSweepThatFindsThem()
    {
        var recorded = await RecordRunAsync(DateTimeOffset.UtcNow);
        var orphan = Guid.NewGuid();
        await PutArtifactAsync(orphan);

        await SweepOrphansAsync();
        Assert.True(factory.ObjectStore.Contains(Bucket, ArtifactKey(orphan)));

        await SweepOrphansAsync();
        Assert.False(factory.ObjectStore.Contains(Bucket, ArtifactKey(orphan)));
        Assert.True(factory.ObjectStore.Contains(Bucket, ArtifactKey(recorded)));
    }

    [Fact]
    public async Task RunRecordedBetweenSweeps_IsKept()
    {
        var jobId = Guid.NewGuid();
        await PutArtifactAsync(jobId);

        await SweepOrphansAsync();
        await RecordRunAsync(DateTimeOffset.UtcNow, jobId);
        await SweepOrphansAsync();
        await SweepOrphansAsync();

        Assert.True(factory.ObjectStore.Contains(Bucket, ArtifactKey(jobId)));
    }

    private async Task SweepOrphansAsync()
    {
        await using var scope = SystemScope();
        await scope.ServiceProvider.GetRequiredService<OrphanedArtifactSweep>().SweepAsync(DateTimeOffset.UtcNow, Ct);
    }

    private async Task<Guid> RecordRunAsync(DateTimeOffset executedAt, Guid? jobId = null)
    {
        var id = jobId ?? Guid.NewGuid();
        if (jobId is null)
        {
            await PutArtifactAsync(id);
        }

        await using var scope = SystemScope();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        db.ExecutionLogs.Add(new ExecutionLogEntry
        {
            JobId = id,
            ExecutedAt = executedAt,
            AgentId = "test-agent",
            CorrelationId = "test",
            Language = ExecutionLanguage.Python,
            Code = "print('hi')",
            Succeeded = true,
            ExitCode = 0,
            Output = "hi",
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = false,
            SentinelAuditId = Guid.NewGuid(),
            DurationMs = 1,
            Artifacts =
            [
                new ArtifactRecord
                {
                    FileName = "out.txt",
                    MimeType = "text/plain",
                    BlobUri = ObjectUri.Create(Bucket, ArtifactKey(id)),
                    SizeBytes = 2
                }
            ]
        });
        await db.SaveChangesAsync(Ct);

        return id;
    }

    private Task PutArtifactAsync(Guid jobId)
    {
        return factory.ObjectStore.PutAsync(Bucket, ArtifactKey(jobId), new MemoryStream([1, 2]), "text/plain", ct: Ct);
    }

    private static string ArtifactKey(Guid jobId) => $"{jobId:N}/out.txt";

    private AsyncServiceScope SystemScope()
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return scope;
    }

    /// <summary>
    /// The monolith with an in-memory object store.
    /// </summary>
    public sealed class Factory : MonolithWebApplicationFactory
    {
        public InMemoryObjectStore ObjectStore { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IObjectStore>();
                services.AddSingleton<IObjectStore>(ObjectStore);
            });
        }
    }
}
