using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Core.Events;
using Infrastructure.Messaging;

namespace Sentinel.Infrastructure.Workers;

public sealed class NetworkSentinelWorker(IEventBus bus, ILogger<NetworkSentinelWorker> logger) : BackgroundService
{
    private const string InboundSubject = "traffic.anomaly";
    private const string OutboundSubject = "events.network.scan";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Network Sentinel Worker started. Listening on {Subject}...", InboundSubject);

        try
        {
            await bus.SubscribeAsync<byte[]>(InboundSubject, async (msg) =>
            {
                try
                {
                    if (msg.Data == null || msg.Data.Length == 0)
                    {
                        return;
                    }

                    using var document = JsonDocument.Parse(msg.Data);
                    var rawData = document.RootElement;

                    if (!rawData.TryGetProperty("score", out var scoreProp))
                    {
                        return;
                    }

                    var mlScore = scoreProp.GetDouble();
                    var zeekData = rawData.GetProperty("data");

                    var sentinelEvent = new SentinelEvent(
                        Source: "Python-ML-Filter",
                        Type: "NetworkAnomaly",
                        Action: mlScore > 0.95 ? "Immediate-Review" : "Investigate",
                        Timestamp: DateTime.UtcNow,
                        Metadata: ExtractMetadata(zeekData, mlScore)
                    );

                    logger.LogInformation("SUCCESS: Received Anomaly from Python! Score: {Score:P0}", mlScore);

                    try
                    {
                        await bus.PublishAsync(OutboundSubject, sentinelEvent, ct: stoppingToken);
                        logger.LogInformation("Publish call returned successfully.");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "The EventBus failed to send the message!");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to parse Python message.");
                }
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Network Sentinel Worker is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Network Sentinel Worker failed fundamentally.");
        }
    }

    private static Dictionary<string, string> ExtractMetadata(JsonElement zeekData, double score)
    {
        string GetSafe(string propertyName)
        {
            return zeekData.TryGetProperty(propertyName, out var element)
                ? element.GetString() ?? "unknown"
                : "unknown";
        }

        return new Dictionary<string, string>
        {
            { "ml_confidence_score", score.ToString("P2") },
            { "orig_h", GetSafe("id.orig_h") },
            { "resp_h", GetSafe("id.resp_h") },
            { "proto", GetSafe("proto") },
            { "service", GetSafe("service") }
        };
    }
}
