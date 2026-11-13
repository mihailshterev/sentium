using System.Text.Json.Serialization;

namespace Sentium.Praxis.Common.Streaming;

public sealed record ToolApprovalData(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("toolName")] string ToolName,
    [property: JsonPropertyName("arguments")] object? Arguments,
    [property: JsonPropertyName("callId")] string? CallId = null);
