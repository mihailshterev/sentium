using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

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
    IHttpClientFactory httpClientFactory,
    IPdpContextAccessor pdpContext,
    ILogger<CodeExecutionSandboxTool> logger) : IAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
            var client = _httpClientFactory.CreateClient(ServiceNames.Sandbox);

            var requestPayload = new
            {
                language,
                code,
                correlationId = _pdpContext.CorrelationId,
                agentId = _pdpContext.AgentName,
                originalUserPrompt = _pdpContext.OriginalUserPrompt
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/sandbox/execute");
            request.Content = JsonContent.Create(requestPayload);
            request.Headers.Add("X-Correlation-ID", _pdpContext.CorrelationId);

            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Sandbox execution returned non-success code: {Status}. Context: {Message}",
                    response.StatusCode, errorMsg);

                return $"Execution Blocked/Failed by Container Host: {response.StatusCode} - {errorMsg}";
            }

            var result = await response.Content.ReadFromJsonAsync<SandboxExecutionResultDto>(cancellationToken: ct);
            if (result is null)
            {
                return "Execution completed but sandbox returned an empty result context.";
            }

            return FormatOutputForAgent(result);
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

    private static string FormatOutputForAgent(SandboxExecutionResultDto result)
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

        if (result.Artifacts?.Length > 0)
        {
            sb.AppendLine("--- Harvested Artifact Blobs ---");
            foreach (var artifact in result.Artifacts)
            {
                sb.AppendLine($"- File: {artifact.FileName} | Storage Link: {artifact.BlobUri}");
            }
        }

        return sb.ToString();
    }

    private sealed class SandboxExecutionResultDto
    {
        public bool Succeeded { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public ArtifactDto[] Artifacts { get; set; } = [];
    }

    private sealed class ArtifactDto
    {
        public string FileName { get; set; } = string.Empty;
        public string BlobUri { get; set; } = string.Empty;
    }
}
