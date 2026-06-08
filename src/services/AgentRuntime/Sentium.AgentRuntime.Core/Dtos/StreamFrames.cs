using System.Text.Json.Serialization;

namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record StreamDoneFrame(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("done")] bool Done = true);

public sealed record StreamErrorFrame(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("type")] string Type = "error",
    [property: JsonPropertyName("done")] bool Done = true);

public sealed record ToolApprovalData(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("toolName")] string ToolName,
    [property: JsonPropertyName("arguments")] object? Arguments);
