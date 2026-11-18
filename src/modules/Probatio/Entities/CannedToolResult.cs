namespace Sentium.Probatio.Entities;

/// <summary>
/// The result every call to <paramref name="ToolName"/> receives during an Evaluation. Tools without one get a
/// neutral stub (ADR 0003).
/// </summary>
internal sealed record CannedToolResult(string ToolName, string Result);
