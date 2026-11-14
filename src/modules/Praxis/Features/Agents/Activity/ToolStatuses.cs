namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// Outcomes of a tool step.
/// </summary>
internal static class ToolStatuses
{
    public const string Running = "running";
    public const string AwaitingApproval = "awaiting_approval";
    public const string Ok = "ok";
    public const string Error = "error";
    public const string Denied = "denied";
    public const string Rejected = "rejected";
}
