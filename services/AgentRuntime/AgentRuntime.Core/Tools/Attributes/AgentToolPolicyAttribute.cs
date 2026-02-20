namespace AgentRuntime.Core.Tools.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AgentToolPolicyAttribute : Attribute
{
    public string[] AllowedAgents { get; init; } = [];
    public ToolRiskLevel RiskLevel { get; init; } = ToolRiskLevel.Low;
    public bool RequiresApproval { get; init; } = false;
}
