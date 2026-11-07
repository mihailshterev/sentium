using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sentium.Codex.Contracts;
using Sentium.Locus.Dtos;
using Sentium.Locus.Features.Attachments;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Locus;

/// <summary>
/// The monolith with Locus' outside dependencies swapped out: blob storage becomes an in-memory store and the
/// knowledge base a substitute (so sync tests can assert exactly what would have been written).
/// </summary>
public sealed class LocusApiFactory : MonolithWebApplicationFactory
{
    public InMemoryAttachmentStore AttachmentStore { get; } = new();

    public IDocumentIngestionService Ingestion { get; } = Substitute.For<IDocumentIngestionService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAttachmentStore>();
            services.AddSingleton<IAttachmentStore>(AttachmentStore);

            services.RemoveAll<IDocumentIngestionService>();
            services.AddSingleton(Ingestion);
        });
    }
}

public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Blobs[blobName] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string blobName, CancellationToken ct = default)
        => Task.FromResult<Stream?>(Blobs.TryGetValue(blobName, out var data) ? new MemoryStream(data, writable: false) : null);

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        Blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in Blobs.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            Blobs.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Small API helpers shared by the Locus integration tests.
/// </summary>
internal static class LocusApi
{
    public static async Task<LocationDto> CreateLocationAsync(this HttpClient client, string name, Guid? parentId = null, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync("/api/locus/locations", new SaveLocationRequest(name, null, null, parentId), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LocationDto>(ct))!;
    }

    public static async Task<AssetDto> CreateAssetAsync(this HttpClient client, SaveAssetRequest request, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync("/api/locus/assets", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetDto>(ct))!;
    }

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 100)];
}
