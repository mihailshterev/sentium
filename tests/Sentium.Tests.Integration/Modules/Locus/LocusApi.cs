using System.Net.Http.Json;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;

namespace Sentium.Tests.Integration.Modules.Locus;

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
