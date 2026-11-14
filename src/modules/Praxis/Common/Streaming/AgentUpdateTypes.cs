namespace Sentium.Praxis.Common.Streaming;

internal static class AgentUpdateTypes
{
    public const string Message = "message";
    public const string Tool = "tool";
    public const string ToolCall = "tool_call";
    public const string ToolResult = "tool_result";
    public const string ApprovalRequest = "approval_request";
    public const string Thought = "thought";
    public const string Done = "done";
    public const string Error = "error";
    public const string Cancelled = "cancelled";
    public const string EnhancedPrompt = "enhanced_prompt";
    public const string Status = "status";
    public const string Title = "title";
}
