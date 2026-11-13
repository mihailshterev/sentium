namespace Sentium.Praxis.Contracts;

/// <summary>
/// Declares how a tool may be used: which agents get it, whether a human must approve each call, and how the Sentinel
/// policy engine classifies the call (<see cref="Resource"/> / <see cref="Action"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AgentToolPolicyAttribute : Attribute
{
    /// <summary>Agents that receive the tool. Empty means every (non-control) agent.</summary>
    public string[] AllowedAgents { get; init; } = [];

    public ToolRiskLevel RiskLevel { get; init; } = ToolRiskLevel.Low;

    /// <summary>When <see langword="true"/> the call is surfaced for human approval before it runs.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>The resource class Sentinel evaluates the call against.</summary>
    public ToolResource Resource { get; init; } = ToolResource.Any;

    /// <summary>The kind of access Sentinel evaluates. Write/Execute calls forward their payload for content inspection.</summary>
    public ToolAction Action { get; init; } = ToolAction.Execute;
}
