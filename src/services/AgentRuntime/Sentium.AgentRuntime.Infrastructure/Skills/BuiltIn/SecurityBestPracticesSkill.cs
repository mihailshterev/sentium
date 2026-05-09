using System.ComponentModel;
using Microsoft.Agents.AI;
using Sentium.AgentRuntime.Core.Skills;

namespace Sentium.AgentRuntime.Infrastructure.Skills.BuiltIn;

/// <summary>
/// Built-in skill providing security best-practice guidance.
/// </summary>
internal sealed class SecurityBestPracticesSkill : AgentClassSkill<SecurityBestPracticesSkill>
{
    internal static BuiltInSkillInfo Descriptor { get; } = new(
        "security-best-practices",
        "Security best practices covering OWASP Top 10, secure coding guidelines, authentication, secrets management, and threat modelling. Use when asked about application security, vulnerabilities, or hardening.",
        """
        Use this skill when the user asks about security, vulnerabilities, secure coding, or threat modelling.

        1. Identify the specific security domain from the user's question (authentication, injection, secrets, etc.).
        2. Load the relevant resource section for detailed guidance.
        3. Give concrete, actionable recommendations with examples where possible.
        4. Always highlight the risk level (Critical / High / Medium / Low).
        5. Reference the relevant OWASP category when applicable.
        """);

    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "security-best-practices",
        "Security best practices covering OWASP Top 10, secure coding guidelines, authentication, secrets management, and threat modelling. Use when asked about application security, vulnerabilities, or hardening.");

    protected override string Instructions => """
        Use this skill when the user asks about security, vulnerabilities, secure coding, or threat modelling.

        1. Identify the specific security domain from the user's question (authentication, injection, secrets, etc.).
        2. Load the relevant resource section for detailed guidance.
        3. Give concrete, actionable recommendations with examples where possible.
        4. Always highlight the risk level (Critical / High / Medium / Low).
        5. Reference the relevant OWASP category when applicable.
        """;

    [AgentSkillResource("owasp-top-10")]
    [Description("OWASP Top 10 2021 vulnerabilities with brief descriptions and remediation guidance.")]
    public string OwaspTop10 => """
        # OWASP Top 10 (2021)

        ## A01 – Broken Access Control (Critical)
        Restrict access to resources based on user roles. Apply least-privilege. Deny by default.
        Validate ownership before serving data. Implement CORS properly.

        ## A02 – Cryptographic Failures (Critical)
        Use TLS 1.2+ everywhere. Encrypt sensitive data at rest (AES-256).
        Never store passwords in plain text — use bcrypt/Argon2/PBKDF2.
        Rotate secrets regularly. Avoid weak algorithms (MD5, SHA-1, DES).

        ## A03 – Injection (Critical)
        Parameterize all SQL queries. Use ORMs with query builders.
        Validate and sanitize all inputs. Apply allowlists, not denylists.

        ## A04 – Insecure Design (High)
        Threat-model before building. Apply defense-in-depth.
        Separate concerns and limit blast radius.

        ## A05 – Security Misconfiguration (High)
        Harden defaults. Remove unused features. Apply security headers.
        Keep dependencies patched. Use structured logging without sensitive data.

        ## A06 – Vulnerable and Outdated Components (High)
        Audit dependencies regularly. Subscribe to CVE feeds.
        Use SCA tools (Dependabot, Snyk). Pin versions and review changelogs.

        ## A07 – Identification and Authentication Failures (High)
        Enforce MFA. Use short-lived tokens (JWT exp).
        Implement brute-force protection. Secure session management.

        ## A08 – Software and Data Integrity Failures (High)
        Verify software supply chain (SBOM). Sign releases.
        Use integrity checks on CI/CD pipelines.

        ## A09 – Security Logging and Monitoring Failures (Medium)
        Log authentication events, access failures, and anomalies.
        Centralise logs. Set up alerting. Retain logs per compliance policy.

        ## A10 – Server-Side Request Forgery (Medium)
        Validate and allowlist outbound URLs. Block internal IP ranges.
        Sanitize user-supplied URLs before making server-side requests.
        """;

    [AgentSkillResource("secrets-management")]
    [Description("Guidance on securely storing and managing secrets, API keys, and credentials.")]
    public string SecretsManagement => """
        # Secrets Management Best Practices

        ## Never Do
        - Hardcode secrets in source code
        - Commit secrets to version control (even private repos)
        - Log secrets or include them in error messages
        - Share secrets via email, Slack, or chat

        ## Storage
        - Use a dedicated secrets manager: Azure Key Vault, HashiCorp Vault, AWS Secrets Manager
        - For local dev: use .env files excluded from git, or dotnet user-secrets
        - Rotate secrets on schedule and immediately after suspected exposure

        ## Access
        - Use managed identities / workload identity where possible (no static credentials)
        - Apply least-privilege: each service gets only the secrets it needs
        - Audit secret access with immutable logs

        ## Transport
        - Always use TLS when transmitting secrets
        - Prefer short-lived tokens (OAuth 2.0 client credentials with short expiry)
        """;
}
