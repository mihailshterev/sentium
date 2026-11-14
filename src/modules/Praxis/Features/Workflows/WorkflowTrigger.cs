using System.Text.Json;

namespace Sentium.Praxis.Features.Workflows;

internal sealed class WorkflowTrigger
{
    public string TriggerType { get; init; } = "";
    public string Payload { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Guid? UserId { get; init; }

    public string StreamId
    {
        get => string.IsNullOrEmpty(field) ? TriggerType : field;
        init;
    }

    /// <summary>
    /// The user's request text: the <c>activity</c> property of a JSON payload, or the raw payload otherwise.
    /// </summary>
    public string ResolveActivity()
    {
        try
        {
            using var doc = JsonDocument.Parse(Payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("activity", out var activity)
                && activity.ValueKind == JsonValueKind.String)
            {
                return activity.GetString() ?? Payload;
            }
        }
        catch (JsonException)
        {
        }

        return Payload;
    }
}
