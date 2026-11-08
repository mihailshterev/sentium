namespace Sentium.Praxis.Dtos;

/// <summary>
/// A tool agents can be given, as listed for the tool picker.
/// </summary>
/// <param name="Name">The tool's function name (see <c>ToolNames</c>).</param>
/// <param name="Description">What the tool does, as the model sees it.</param>
/// <param name="Resource">The resource class it touches (<c>ToolResource</c>: File, VectorDb, Memory, Code, ...).</param>
/// <param name="Action">The kind of access (<c>ToolAction</c>: Read, Search, Write, Execute).</param>
/// <param name="Risk">The declared risk level (<c>ToolRiskLevel</c>).</param>
/// <param name="RequiresApproval">Whether every call waits for a human (such tools are skipped in unattended workflow runs).</param>
/// <param name="Server">For MCP tools, the display name of the MCP server providing it; <see langword="null"/> for built-in tools.</param>
public sealed record ToolDescriptor(string Name, string Description, string Resource, string Action, string Risk, bool RequiresApproval, string? Server = null);
