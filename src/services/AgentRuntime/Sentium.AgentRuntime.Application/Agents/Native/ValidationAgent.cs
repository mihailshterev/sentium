using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Application.Agents.Native;

/// <summary>
/// A high-level native agent that acts as a quality gate and final reviewer.
/// The ValidationAgent synthesizes inputs from multiple domain experts to provide
/// a unified, risk-assessed conclusion for the end user.
/// </summary>
public sealed class ValidationAgent : IAgent
{
    /// <inheritdoc />
    /// <value>"Validator"</value>
    public string Name => "Validator";

    /// <inheritdoc />
    /// <remarks>
    /// This agent enforces a specific output schema (SUMMARY, RISK, RECOMMENDATION).
    /// It serves as the final step in a security investigation workflow, ensuring that
    /// technical findings from agents like <see cref="ForensicsAgent"/> and
    /// <see cref="SecurityAnalyst"/> are actionable and categorized by severity.
    /// </remarks>
    public string Instructions => @"You are a Senior Security Reviewer.
    Your task is to review the findings provided by a squad of specialized agents.

    1. Summarize the collective findings.
    2. Assess the 'Risk Level' (Low, Medium, High, Critical).
    3. Provide a clear 'Final Recommendation'.

    You must output your response in a structured format:
    SUMMARY: [Text]
    RISK: [Level]
    RECOMMENDATION: [Text]";
}
