using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;

namespace Sentium.AgentRuntime.Infrastructure.Learnings;

/// <summary>
/// Three-stage, fail-closed gate that decides whether a captured learning may be shared globally:
/// <list type="number">
/// <item>Deterministic identifier guard — regex rejection of user-specific identifiers.</item>
/// <item>LLM generalizability judge — confirms the content is an abstracted, reusable pattern.</item>
/// <item>Semantic dedup — skips promotion when an equivalent global learning already exists.</item>
/// </list>
/// Any unexpected failure results in <c>Approved = false</c> so a learning is never wrongly globalized.
/// </summary>
public sealed partial class LearningSanitizationPipeline(
    IChatClient chatClient,
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    ILogger<LearningSanitizationPipeline> logger) : ILearningSanitizationPipeline
{
    private const string LearningsCollection = "agent_learnings";

    /// <summary>
    /// Cosine similarity at or above which a candidate is treated as a duplicate of existing global knowledge.
    /// </summary>
    private const float DuplicateThreshold = 0.92f;

    public async Task<LearningPromotionVerdict> EvaluateForGlobalAsync(string content, string tags, string agentName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new LearningPromotionVerdict(false, "Empty content cannot be shared globally.", content ?? string.Empty);
        }

        // Stage 1 — deterministic identifier guard (hard reject, independent of the LLM).
        if (DetectIdentifier(content) is { } category)
        {
            return new LearningPromotionVerdict(false, $"Contains a user-specific identifier ({category}); kept private.", content);
        }

        try
        {
            // Stage 2 — LLM generalizability judge (un-harnessed client: no agent tools/governance).
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, LearningSanitizationPrompt.SystemRole),
                new(ChatRole.User, LearningSanitizationPrompt.BuildCandidate(agentName, tags, content))
            };

            var response = await chatClient.GetResponseAsync(messages, new ChatOptions { Temperature = 0f }, ct);

            var verdict = LlmParser.ParseSanitizationVerdict(response.Text ?? string.Empty, content);
            if (!verdict.Approved)
            {
                return verdict;
            }

            if (DetectIdentifier(verdict.SanitizedContent) is { } leaked)
            {
                return new LearningPromotionVerdict(false, $"Sanitized text still contains a user-specific identifier ({leaked}); kept private.", content);
            }

            // Stage 3 — semantic dedup against existing GLOBAL learnings only.
            if (await FindGlobalDuplicateAsync(verdict.SanitizedContent, ct) is { } duplicateId)
            {
                return verdict with
                {
                    Reason = "An equivalent global learning already exists; not duplicated.",
                    DuplicateOfId = duplicateId
                };
            }

            return verdict;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Learning sanitization failed; keeping the learning private.");
            return new LearningPromotionVerdict(false, "Validation could not be completed; kept private.", content);
        }
    }

    private async Task<Guid?> FindGlobalDuplicateAsync(string content, CancellationToken ct)
    {
        var embedding = await embeddingService.GenerateEmbeddingAsync(content, ct);

        var globalScope = new KnowledgeScopeFilter(null);

        var hits = await vectorRepository.SearchAsync(
            LearningsCollection,
            embedding,
            topK: 1,
            scoreThreshold: DuplicateThreshold,
            scope: globalScope,
            ct: ct);

        if (hits.Count == 0)
        {
            return null;
        }

        var match = hits[0].Chunk;
        if (match.Metadata.TryGetValue("learning_id", out var idText) && Guid.TryParse(idText, out var id))
        {
            return id;
        }

        return Guid.Empty;
    }

    private static string? DetectIdentifier(string text)
    {
        if (EmailRegex().IsMatch(text)) return "email address";
        if (WindowsPathRegex().IsMatch(text)) return "Windows file path";
        if (UncPathRegex().IsMatch(text)) return "UNC network path";
        if (UnixHomePathRegex().IsMatch(text)) return "home directory path";
        if (CredentialUrlRegex().IsMatch(text)) return "URL with credentials";
        if (IpAddressRegex().IsMatch(text)) return "IP address";
        return null;
    }

    // user@host.tld
    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    // C:\Users\name\..., D:\path\..., etc.
    [GeneratedRegex(@"[A-Za-z]:\\[^\s""']+", RegexOptions.None)]
    private static partial Regex WindowsPathRegex();

    // \\server\share
    [GeneratedRegex(@"\\\\[^\s\\""']+\\[^\s""']+", RegexOptions.None)]
    private static partial Regex UncPathRegex();

    // /home/alice, /Users/alice, /root
    [GeneratedRegex(@"(?:/home/|/Users/|/root\b)[^\s""']*", RegexOptions.IgnoreCase)]
    private static partial Regex UnixHomePathRegex();

    // scheme://user:pass@host
    [GeneratedRegex(@"[a-z][a-z0-9+.-]*://[^\s/@""']+:[^\s/@""']+@", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialUrlRegex();

    // IPv4 (a deliberately simple form; IPv6 covered by the colon-hextet pattern)
    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b|\b(?:[A-Fa-f0-9]{1,4}:){2,}[A-Fa-f0-9]{1,4}\b", RegexOptions.None)]
    private static partial Regex IpAddressRegex();
}
