using System.Net.Http.Json;
using System.Text;
using Sentium.Locus.Core.Dtos;
using Sentium.Locus.Core.Ingestion;
using Microsoft.Extensions.Logging;

namespace Sentium.Locus.Infrastructure.Ingestion;

/// <summary>
/// Typed HTTP client that forwards asset data to the AgentRuntime ingestion API,
/// making agent-accessible assets available as RAG context.
/// </summary>
public sealed class AgentIngestionClient(HttpClient httpClient, ILogger<AgentIngestionClient> logger) : IAgentIngestionClient
{
    /// <summary>
    /// Builds a stable source identifier for an asset so its vectors can be found and replaced/removed.
    /// </summary>
    internal static string AssetSource(Guid assetId) => $"locus:asset:{assetId}";

    public async Task IngestAssetAsync(AssetDto asset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var source = AssetSource(asset.Id);
        var content = BuildContent(asset);

        var payload = new
        {
            content,
            source,
            sourceType = "InventoryService",
            metadata = new Dictionary<string, string>
            {
                ["assetId"] = asset.Id.ToString(),
                ["locationId"] = asset.LocationId.ToString(),
                ["category"] = asset.Category ?? string.Empty
            }
        };

        var response = await httpClient.PostAsJsonAsync("ingestion", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AgentRuntime ingestion returned {Status} for asset {AssetId}", response.StatusCode, asset.Id);
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Ingested asset {AssetId} ({Name}) into knowledge base", asset.Id, asset.DisplayName);
        }
    }

    public async Task RemoveAssetAsync(Guid assetId, CancellationToken ct = default)
    {
        var source = AssetSource(assetId);
        var response = await httpClient.DeleteAsync($"ingestion?source={Uri.EscapeDataString(source)}", ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AgentRuntime removal returned {Status} for asset {AssetId}", response.StatusCode, assetId);
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Removed asset {AssetId} vectors from knowledge base", assetId);
        }
    }

    private static string BuildContent(AssetDto a)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Asset: {a.DisplayName}");
        sb.AppendLine($"Location: {a.LocationName}");

        if (!string.IsNullOrWhiteSpace(a.Category))
        {
            sb.AppendLine($"Category: {a.Category}");
        }

        if (!string.IsNullOrWhiteSpace(a.PhysicalDescription))
        {
            sb.AppendLine($"Physical Description: {a.PhysicalDescription}");
        }

        if (!string.IsNullOrWhiteSpace(a.Manufacturer))
        {
            sb.AppendLine($"Manufacturer: {a.Manufacturer}");
        }

        if (!string.IsNullOrWhiteSpace(a.ModelNumber))
        {
            sb.AppendLine($"Model: {a.ModelNumber}");
        }

        if (!string.IsNullOrWhiteSpace(a.SerialNumber))
        {
            sb.AppendLine($"Serial Number: {a.SerialNumber}");
        }

        if (a.PurchaseDate.HasValue)
        {
            sb.AppendLine($"Purchase Date: {a.PurchaseDate.Value:d}");
        }

        if (a.LastServicedDate.HasValue)
        {
            sb.AppendLine($"Last Serviced: {a.LastServicedDate.Value:d}");
        }

        if (!string.IsNullOrWhiteSpace(a.WarrantyInfo))
        {
            sb.AppendLine($"Warranty: {a.WarrantyInfo}");
        }

        if (!string.IsNullOrWhiteSpace(a.Instructions))
        {
            sb.AppendLine($"User Instructions: {a.Instructions}");
        }

        if (!string.IsNullOrWhiteSpace(a.AgentInstructions))
        {
            sb.AppendLine($"Agent Instructions: {a.AgentInstructions}");
        }

        return sb.ToString();
    }
}
