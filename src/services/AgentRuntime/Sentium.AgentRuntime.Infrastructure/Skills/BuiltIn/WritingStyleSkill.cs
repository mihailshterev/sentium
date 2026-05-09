using System.ComponentModel;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill providing writing style and communication guidelines.
/// </summary>
internal sealed class WritingStyleSkill : AgentClassSkill<WritingStyleSkill>
{
    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "writing-style",
        "Writing style guidance for technical documentation, reports, and professional communication. Use when drafting documents, reviewing writing, or asking about tone, grammar, or structure.",
        """
        Use this skill when the user asks for help with writing, editing, or communicating ideas clearly.

        1. Identify the document type: technical doc, executive summary, email, or report.
        2. Consult the style-guide resource for the appropriate conventions.
        3. Apply the golden rules: clarity, brevity, active voice, and reader focus.
        4. Suggest concrete rewrites for unclear sentences.
        5. For technical writing: prefer short sentences, avoid jargon without definition, and use examples.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "writing-style",
        "Writing style guidance for technical documentation, reports, and professional communication. Use when drafting documents, reviewing writing, or asking about tone, grammar, or structure.");

    protected override string Instructions => """
        Use this skill when the user asks for help with writing, editing, or communicating ideas clearly.

        1. Identify the document type: technical doc, executive summary, email, or report.
        2. Consult the style-guide resource for the appropriate conventions.
        3. Apply the golden rules: clarity, brevity, active voice, and reader focus.
        4. Suggest concrete rewrites for unclear sentences.
        5. For technical writing: prefer short sentences, avoid jargon without definition, and use examples.
        """;

    [AgentSkillResource("style-guide")]
    [Description("Core writing style rules for technical and professional documents.")]
    public string StyleGuide => """
        # Writing Style Guide

        ## Golden Rules
        1. **Clarity over cleverness** — If a reader needs to re-read a sentence, rewrite it.
        2. **Active voice** — "The system processes the request" not "The request is processed by the system."
        3. **Short sentences** — Aim for ≤ 20 words per sentence. Split complex ideas.
        4. **One idea per paragraph** — State it, support it, conclude it.
        5. **Reader-first** — Answer "what does the reader need?" before you write.

        ## Document Structure
        - Lead with the most important information (inverted pyramid).
        - Use headings and bullet points for scannable content.
        - Include a TL;DR or summary for documents over 500 words.
        - Use numbered lists for sequential steps, bullets for unordered items.

        ## Technical Documentation
        - Define acronyms on first use: "BFF (Backend-for-Frontend)".
        - Include code examples for every API endpoint or configuration option.
        - Keep version / date metadata visible.
        - Test all steps you document.

        ## Tone
        - Professional but not stiff.
        - Direct: avoid "please note that", "it should be noted", "in order to".
        - Inclusive: avoid gendered defaults, prefer "they" for unknown individuals.

        ## Common Mistakes to Avoid
        | Wrong                      | Right                      |
        |----------------------------|----------------------------|
        | "utilize"                  | "use"                      |
        | "in order to"              | "to"                       |
        | "due to the fact that"     | "because"                  |
        | Passive: "it was decided"  | Active: "we decided"       |
        | Jargon without definition  | Define on first use        |
        """;

    [AgentSkillResource("email-templates")]
    [Description("Starter templates for common professional email types.")]
    public string EmailTemplates => """
        # Professional Email Templates

        ## Status Update
        Subject: [Project] Status Update – [Date]
        Body:
        Hi [Team/Name],
        Quick update on [project]:
        - Completed: [item]
        - In progress: [item] (ETA: [date])
        - Blocker: [item] — need [action]
        Next check-in: [date].
        [Name]

        ## Request / Ask
        Subject: [Clear, specific request]
        Body:
        Hi [Name],
        Context: [one sentence background].
        Request: [exactly what you need].
        Why: [brief reason / impact].
        By when: [date/time].
        Let me know if you have questions.
        [Name]

        ## Incident Notification
        Subject: [SEV-X] [Service] Issue – [Start Time]
        Body:
        **Status**: Investigating / Mitigated / Resolved
        **Impact**: [what is affected and how many users]
        **Start time**: [ISO 8601]
        **Current action**: [what is being done]
        **Next update**: [time]
        """;
}
