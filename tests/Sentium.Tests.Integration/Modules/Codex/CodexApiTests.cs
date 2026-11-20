using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Codex.Data;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Codex;

public sealed class CodexApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetKnowledgeBaseStats_ReturnsStatsForEveryPopulatedCollection()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
            foreach (var collection in KnowledgeCollections.All)
            {
                var embedding = new float[CodexDbContext.EmbeddingDimensions];
                embedding[0] = 1f;
                await repo.UpsertAsync(collection, new DocumentChunk { Content = "c", Source = $"stats-test-{Guid.NewGuid()}" }, embedding, Ct);
            }
        }

        var response = await _client.GetAsync("/api/codex/knowledge-base/stats", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<List<CollectionStats>>(Ct);
        Assert.Equivalent(KnowledgeCollections.All, stats!.Select(s => s.CollectionName));
        Assert.All(stats!, s => Assert.True(s.PointCount >= 1 && s.VectorSize == CodexDbContext.EmbeddingDimensions));
    }
}
