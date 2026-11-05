using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sentium.Praxis.Features.CodeExecution;
using Sentium.Praxis.Features.Tools.Attributes;
using Sentium.Praxis.Features.Sentinel;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Autonomous execution gateway that links the AI Agent session to the Sandbox service isolation sandboxes.
/// This tool forces code execution requests to clear the Sentinel PDP guard rails and user approvals
/// before hitting the backend endpoint.
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.High,
    RequiresApproval = true)]
public sealed class CodeExecutionSandboxTool(
    ISandboxClient sandboxClient,
    IPdpContextAccessor pdpContext,
    ILogger<CodeExecutionSandboxTool> logger) : IAgentTool
{
    private readonly ISandboxClient _sandboxClient = sandboxClient ?? throw new ArgumentNullException(nameof(sandboxClient));
    private readonly IPdpContextAccessor _pdpContext = pdpContext ?? throw new ArgumentNullException(nameof(pdpContext));
    private readonly ILogger<CodeExecutionSandboxTool> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string Name => "execute_code_sandbox";

    public string Description => "Executes code (Python or Node.js) safely within an isolated, ephemeral sandbox environment. USE NO EXTERNAL LIBRARIES. ONLY NATIVE LANGUAGE ONES.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } =
    [
        new("language", "The script language.", EnumValues: ["Python", "Node"]),
        new("code", "The source code to execute. USE NO EXTERNAL LIBRARIES. ONLY NATIVE LANGUAGE ONES."),
    ];

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Execution failed: No configuration payload provided.";
        }

        var (language, code) = ParseInput(input);

        if (string.IsNullOrWhiteSpace(code))
        {
            return "Execution failed: Source code script cannot be null or empty.";
        }

        _logger.LogInformation(
            "Agent triggering isolated sandbox container execution. Language: {Language}, CorrelationId: {CorrelationId}",
            language, _pdpContext.CorrelationId);

        try
        {
            var correlationId = _pdpContext.CorrelationId.Length > 0 ? _pdpContext.CorrelationId : Guid.NewGuid().ToString();

            var result = await _sandboxClient.ExecuteAsync(new SandboxExecutionRequest
            {
                Language = language,
                Code = code,
                AgentId = _pdpContext.AgentName,
                OriginalUserPrompt = _pdpContext.OriginalUserPrompt
            }, correlationId, ct);

            if (result.PolicyDenied)
            {
                _logger.LogWarning("Sandbox execution denied by security policy. Reason: {Reason}", result.PolicyDenialReason);

                return $"Execution Blocked/Failed by Container Host: Forbidden - Execution denied by security policy: {result.PolicyDenialReason}";
            }

            return FormatOutputForAgent(result);
        }
        catch (SandboxRejectedException ex)
        {
            _logger.LogWarning(
                "Sandbox execution returned non-success code: {Status}. Context: {Message}",
                ex.StatusCode, ex.Details);

            return $"Execution Blocked/Failed by Container Host: {ex.StatusCode} - {ex.Details}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal failure routing script execution payload to the sandbox cluster.");
            return $"Critical structural error executing code: {ex.Message}";
        }
    }

    private static (string language, string code) ParseInput(string input)
    {
        var trimmed = input.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            var language = "Python";
            if (root.TryGetProperty("language", out var lang))
            {
                language = lang.GetString() ?? "Python";
            }

            var code = string.Empty;
            if (root.TryGetProperty("code", out var c))
            {
                code = c.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("script", out var s))
            {
                code = s.GetString() ?? string.Empty;
            }

            return (language, code);
        }
        catch (JsonException)
        {
            return ("Python", trimmed);
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
