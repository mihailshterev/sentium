using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.CodeExecution;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Runs agent-written code in the Sandbox. Calls need user approval and pass the Sentinel PDP before they reach it.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.High, RequiresApproval = true, Resource = ToolResource.Code, Action = ToolAction.Execute)]
public sealed class CodeExecutionSandboxTool(
    ISandboxClient sandboxClient,
    IPdpContextAccessor pdpContext,
    ILogger<CodeExecutionSandboxTool> logger) : IAgentTool
{
    private const string NoExternalLibraries = "USE NO EXTERNAL LIBRARIES. ONLY NATIVE LANGUAGE ONES.";

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ExecuteAsync,
            ToolNames.ExecuteCode,
            $"Executes code (Python or Node.js) safely within an isolated, ephemeral sandbox environment. {NoExternalLibraries}");
    }

    public async Task<string> ExecuteAsync(
        [Description($"The source code to execute. {NoExternalLibraries}")] string code,
        [Description("The script language.")] ExecutionLanguage language = ExecutionLanguage.Python,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Execution failed: Source code script cannot be null or empty.";
        }

        logger.LogInformation(
            "Agent triggering isolated sandbox container execution. Language: {Language}, CorrelationId: {CorrelationId}",
            language, pdpContext.CorrelationId);

        try
        {
            var correlationId = pdpContext.CorrelationId.Length > 0 ? pdpContext.CorrelationId : Guid.NewGuid().ToString();

            var result = await sandboxClient.ExecuteAsync(new SandboxExecutionRequest
            {
                Language = language.ToString(),
                Code = code,
                AgentId = AgentToolFunction.CurrentAgentName,
                OriginalUserPrompt = pdpContext.OriginalUserPrompt
            }, correlationId, ct);

            if (result.PolicyDenied)
            {
                logger.LogWarning("Sandbox execution denied by security policy. Reason: {Reason}", result.PolicyDenialReason);

                return $"Execution Blocked/Failed by Container Host: Forbidden - Execution denied by security policy: {result.PolicyDenialReason}";
            }

            return FormatOutputForAgent(result);
        }
        catch (SandboxRejectedException ex)
        {
            logger.LogWarning(
                "Sandbox execution returned non-success code: {Status}. Context: {Message}",
                ex.StatusCode, ex.Details);

            return $"Execution Blocked/Failed by Container Host: {ex.StatusCode} - {ex.Details}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal failure routing script execution payload to the sandbox cluster.");
            return $"Critical structural error executing code: {ex.Message}";
        }
    }

    private static string FormatOutputForAgent(SandboxExecutionResponse result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Sandbox Containment Finished - Exit Code: {result.ExitCode}]");
        sb.AppendLine($"Succeeded : {result.Succeeded}");
        sb.AppendLine($"Duration  : {result.DurationMs}ms");

        if (!string.IsNullOrEmpty(result.Output))
        {
            sb.AppendLine("--- Standard Output ---");
            sb.AppendLine(result.Output);
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            sb.AppendLine("--- Error Stream ---");
            sb.AppendLine(result.Error);
        }

        if (result.Artifacts.Count > 0)
        {
            sb.AppendLine("--- Harvested Artifact Blobs ---");
            foreach (var artifact in result.Artifacts)
            {
                sb.AppendLine($"- File: {artifact.FileName} | Storage Link: {artifact.BlobUri}");
            }
        }

        return sb.ToString();
    }
}
