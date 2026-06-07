# Sentium - Threat Model

> Security threat model for the Sentium self-hosted AI agent platform. Uses the
> [STRIDE](https://en.wikipedia.org/wiki/STRIDE_model) methodology over the system's trust
> boundaries. This document records the threats considered, the mitigations actually built into
> the system, and the residual risks accepted under the project's deployment model.
>
> Companion documents: [docs/final-state-overview.md](docs/final-state-overview.md) (architecture
> as built).

---

## 1. Scope & Deployment Model

Sentium is a **self-hosted, privacy-first AI agent platform** intended to run **entirely within a
single trusted host or private network**, operated by one administrator (`Sovereign`). All AI
inference is local (Ollama); there is no external cloud dependency.

This deployment model is central to the threat model:

- The **only publicly reachable entry point is the Gateway (BFF)**. All other services
  (AgentRuntime, Identity, Registry, Sandbox, Sentinel, Watchdog) and all infrastructure
  (SQL Server, Redis, NATS, Qdrant, Ollama, Seq, Azurite) live on a **trusted internal network**
  and are not exposed to untrusted clients.
- The trust model assumes **no untrusted tenant** sharing the internal network. Multi-tenant
  hostile-network deployment is explicitly out of scope (see §8 Residual Risks).

### Primary assets

| Asset                                                                   | Why it matters                                            |
| ----------------------------------------------------------------------- | --------------------------------------------------------- |
| User credentials & session cookies                                      | Account takeover                                          |
| OIDC tokens (access / refresh / id)                                     | Impersonation, privilege escalation                       |
| Per-user data (conversations, RAG memories, knowledge base, workspaces) | Privacy - the platform's core promise                     |
| `X-Internal-Token` pre-shared key                                       | Bypasses Sentinel & reaches privileged internal endpoints |
| Sentinel audit log                                                      | Accountability / forensic integrity                       |
| The host running the Sandbox                                            | Container escape → full host compromise                   |
| Runtime configuration (Registry)                                        | Disabling governance (autonomy level, policy toggles)     |
| LLM models & inference                                                  | Resource exhaustion; manipulated outputs                  |

### Actors

- **Anonymous / external attacker** - reaches only the Gateway.
- **Authenticated `Member`** - standard user; their actions are user-scoped and policy-governed.
- **`Sovereign`** - administrator; trusted, can read audit, change config, bypass data filters.
- **The AI agent itself** - a _semi-trusted, potentially-compromised_ actor. Prompt injection,
  hallucination, or a malicious skill can make an agent attempt unauthorized actions. Much of the
  governance machinery (Sentinel, Sandbox) exists to bound this actor.
- **Malicious/buggy code** submitted for Sandbox execution.

---

## 2. Trust Boundaries

```
 [ Browser ]  --- httpOnly cookie only, never tokens
      |  (public)
 ===== TB1: public edge ==========================================
      v
 [ Gateway / BFF (YARP) ]  --- sole OIDC client; holds tokens in encrypted cookie
      |  (JWT Bearer, internal network)
 ===== TB2: authenticated service mesh ===========================
      v
 [ Identity ] [ AgentRuntime ] [ Registry ] [ Watchdog ]
      |
      |  (X-Internal-Token, SystemCaller policy)
 ===== TB3: privileged internal endpoints ========================
      v
 [ Sentinel /policy/evaluate ]   [ Sandbox /sandbox/execute ]
                                       |
 ===== TB4: untrusted code execution =============================
                                       v
                            [ Ephemeral hardened Docker container ]
```

- **TB1 - Public edge.** The only boundary exposed to untrusted input. Everything past it assumes
  a trusted network.
- **TB2 - Authenticated mesh.** Services validate JWTs minted by Identity; per-user query filters
  enforce data isolation.
- **TB3 - Privileged internal endpoints.** Sentinel policy evaluation and Sandbox execution accept
  only internal callers bearing `X-Internal-Token`. Never reachable from the browser.
- **TB4 - Untrusted code.** Agent-submitted code runs inside an ephemeral, network-disabled,
  capability-stripped container. The boundary here is the container/kernel.

---

## 3. STRIDE Analysis by Boundary

### 3.1 Spoofing (identity)

| Threat                                        | Mitigation                                                                                                                                                                                               |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Attacker steals tokens from the browser       | **BFF pattern** - the browser holds only an `HttpOnly`, `Secure` cookie (`Sentium.Auth`). Tokens never reach JavaScript; XSS cannot exfiltrate them.                                                     |
| Forged / replayed auth code                   | **Authorization Code Flow + PKCE**; back-channel code→token exchange with client secret.                                                                                                                 |
| Service impersonation on the internal network | Services validate **JWT against the Identity authority**; privileged endpoints additionally require **`X-Internal-Token`** (validated by `InternalApiKeyAuthenticationHandler` → `SystemCaller` policy). |
| Credential stuffing / brute force on login    | **Rate limiting and account lockout** on the `auth` path in Identity.                                                                                                                                    |
| Agent claims to be a system caller            | Internal token is an **Aspire-generated per-install secret**, not a fixed default; injected only by `InternalApiKeyDelegatingHandler` on trusted service-to-service calls.                               |

### 3.2 Tampering (integrity)

| Threat                                             | Mitigation                                                                                                                                                  |
| -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Tampering with session cookie                      | Cookie is **encrypted + signed** by the Gateway (ASP.NET Core data protection); `SaveTokens` keeps tokens server-side-in-cookie, not client-modifiable.     |
| Modifying another user's data                      | **EF Core global query filters** scope every user-owned entity by `UserId`; cache keys are user-prefixed.                                                   |
| Tampering with runtime config to weaken governance | Registry changes are an authenticated, `Sovereign`-gated operation; config is the Sovereign's responsibility (trusted actor).                               |
| Tampering with the audit log                       | Audit is **append-only, written by the policy engine**; read access is `Sovereign`-only.                                                                    |
| Man-in-the-middle on internal calls                | Mitigated by **network isolation** (defence-in-depth), with the internal token as an additional layer. mTLS is the noted upgrade path for hostile networks. |

### 3.3 Repudiation (accountability)

| Threat                                 | Mitigation                                                                                                                                    |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Agent/user denies performing an action | **Sentinel writes a forensic audit record for every `/policy/evaluate`** decision (allow/deny, risk, alignment verdict, duration, `auditId`). |
| Sandbox execution disowned             | Every execution is logged (code, agent, prompt, exit code, artifacts, originating `auditId`) before and after running.                        |
| No centralized visibility              | **Seq** aggregates structured logs across all services.                                                                                       |

### 3.4 Information Disclosure (confidentiality)

| Threat                             | Mitigation                                                                                                                                             |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Cross-user data leakage            | Per-user isolation via query filters; **`Sovereign`/background bypass is explicit** (`SovereignDataAccessor` / `SystemUserContext`), never accidental. |
| Tokens leaked to frontend          | BFF pattern - frontend never sees tokens.                                                                                                              |
| Direct blob-storage access         | Artifacts are **proxied through the backend**; the browser never gets direct blob-store URIs.                                                          |
| Data exfiltration by executed code | Sandbox containers run with **`NetworkDisabled`** - no outbound network at all.                                                                        |
| RAG/memory leakage across scopes   | Knowledge base distinguishes **private vs shared scopes**; retrieval is user-scoped.                                                                   |
| Sensitive data sent to a cloud LLM | **Local inference only** (Ollama); no provider calls leave the host.                                                                                   |

### 3.5 Denial of Service (availability)

| Threat                                           | Mitigation                                                                                                                                                                   |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Compromised agent floods actions                 | **`RateLimitingPolicy`** (sliding window per agent) is the first Sentinel barrier - limits the "blast radius".                                                               |
| Runaway / malicious code consumes host resources | Sandbox containers have **hard CPU & memory limits, `PidsLimit`, `nofile`/`nproc` ulimits, output size caps, and an execution timeout** (killed on overrun).                 |
| LLM judge hangs the policy pipeline              | `SemanticIntentPolicy` timeouts/errors return **`INCONCLUSIVE`** rather than blocking - availability preserved without silently allowing misaligned actions at low autonomy. |
| Cascading service failure                        | **Global Polly resilience** (retry + timeout + circuit breaker) on all HttpClients; Watchdog monitors service health.                                                        |

### 3.6 Elevation of Privilege

| Threat                                                | Mitigation                                                                                                                                                                                   |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Member` performs `Sovereign`-only actions            | Two-tier role model enforced at the API; audit/config/bypass are `Sovereign`-gated.                                                                                                          |
| Browser reaches privileged internal endpoints         | `/policy/evaluate` and `/sandbox/execute` require `X-Internal-Token`; **not routed through public Gateway surface**.                                                                         |
| Agent escalates beyond user intent (prompt injection) | **`SemanticIntentPolicy`** compares the declared action against the original user prompt via a local LLM (temp 0). `MISALIGNED` → deny-with-alert. Behavior tightens at low `AutonomyLevel`. |
| **Container escape → host compromise**                | Defence-in-depth hardening (see §4).                                                                                                                                                         |
| Policy bypass via unhandled exception                 | Sentinel is **fail-closed**: any unhandled error in any policy → deny with critical risk.                                                                                                    |

---

## 4. Sandbox: Untrusted Code Execution (highest-risk boundary)

Executing agent-generated code is the single highest-risk capability. Two-phase: **Sentinel must
authorize before any container starts** (fail-closed), then `DockerSandboxRunner` runs it under
aggressive constraints set by `ContainerConfigBuilder`:

| Control                 | Setting / effect                                                            |
| ----------------------- | --------------------------------------------------------------------------- |
| Network                 | `NetworkDisabled` - no network access at all                                |
| Filesystem              | `ReadonlyRootfs`; `/tmp` is `tmpfs` with `noexec,nosuid,nodev`              |
| User                    | `nobody` (unprivileged)                                                     |
| Linux capabilities      | `CapDrop: ALL`, `CapAdd: []`                                                |
| Privileges              | `no-new-privileges:true`; `Privileged=false`; seccomp profile               |
| CPU / memory            | hard limits (`NanoCPUs`, `Memory` = `MemorySwap`, i.e. swap disabled)       |
| Processes / descriptors | `PidsLimit`, `nofile` / `nproc` ulimits                                     |
| Output / time           | bounded stdout/stderr size; execution timeout (container killed on overrun) |
| Lifecycle               | container + working directory cleaned up guaranteed (`finally`)             |

**Residual risk:** a kernel/Docker 0-day enabling container escape would compromise the host. This
is inherent to running untrusted code on shared kernel containers; mitigated by hardening,
local-only deployment, and keeping the Docker host patched. A VM/gVisor/Firecracker isolation layer
is the stronger upgrade path.

---

## 5. AI-Specific Threats

The agent is treated as a **semi-trusted actor**. AI-specific risks beyond classic STRIDE:

| Threat                                                                                                              | Mitigation / status                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Prompt injection** (via documents, RAG content, tool output, user input) steering the agent to unintended actions | `SemanticIntentPolicy` alignment check; Sentinel gates every tool call & code execution; sensitive tools also require explicit user approval.                                                         |
| **Excessive autonomy**                                                                                              | Configurable `AutonomyLevel`; at very high autonomy the alignment check is skipped (an accepted, operator-chosen trade-off), at low autonomy uncertain actions are denied.                            |
| **Hallucinated / unsafe actions**                                                                                   | Same PDP gating; network-isolated, reversible sandboxed execution.                                                                                                                                    |
| **Data poisoning of the self-improvement loop**                                                                     | Auto-injected learnings (`LearningAugmentedChatClient`) are user-scoped; prompt enhancement is optional/configurable. _Note: poisoned learnings could degrade future responses - see residual risks._ |
| **Model/prompt extraction**                                                                                         | Limited exposure - local models, no external API; system prompts are server-side.                                                                                                                     |
| **Resource exhaustion via inference**                                                                               | Rate limiting + per-action governance; local resource limits.                                                                                                                                         |

---

## 6. Infrastructure & Dependencies

| Component                           | Threat                                  | Note                                                                                                                          |
| ----------------------------------- | --------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| SQL Server (5 isolated DBs)         | Cross-DB data merge / over-broad access | DBs are **never shared or merged**; one owner service each.                                                                   |
| Redis (HybridCache L2)              | Cache poisoning / stale auth data       | Internal network only; NATS-broadcast invalidation keeps tiers consistent.                                                    |
| NATS JetStream                      | Unauthorized message publish/subscribe  | Internal network only; trusted services.                                                                                      |
| Qdrant / Ollama / Azurite / Seq     | Direct access bypassing app controls    | Internal network only; not publicly exposed.                                                                                  |
| Third-party packages (NuGet / pnpm) | Supply-chain compromise                 | Pinned lockfiles; keep dependencies patched (see §7).                                                                         |
| Secrets in source/containers        | Leakage of keys/connection strings      | Use `.env` / Aspire secrets (`.env.example` is the template); **never commit real secrets**. `.gitignore` excludes env files. |

---

## 7. Recommendations / Hardening Backlog

These are not yet fully addressed and are worth tracking:

1. **Dependency scanning** in CI (Dependabot / `dotnet list package --vulnerable`, `pnpm audit`).
2. **Stronger sandbox isolation** (gVisor / microVM) if untrusted code volume grows.
3. **mTLS or per-service `client_credentials` tokens** if Sentium is ever deployed on a network
   with untrusted peers (Identity already supports `client_credentials`).
4. **Security headers / CSP** review on the Gateway and identity UI to harden against XSS.
5. **Audit-log tamper-evidence** (e.g. hash chaining) if forensic guarantees must survive a
   compromised host.
6. **Provenance/validation for RAG-ingested content** to reduce learning-loop poisoning.
7. **Secret rotation** procedure for the `X-Internal-Token` and OIDC client secret.

---

## 8. Residual / Accepted Risks

Accepted under the self-hosted, single-operator, trusted-network deployment model:

- **Trusted internal network assumption.** A `X-Internal-Token` pre-shared key (not mTLS) protects
  internal endpoints. Acceptable as _defence-in-depth on top of_ network isolation; inadequate for
  a hostile/multi-tenant network (upgrade path: mTLS).
- **Trusted `Sovereign`.** The administrator can read all audit, change governance config, and
  bypass data isolation. There is no protection against a malicious Sovereign - they own the host.
- **Container escape via kernel 0-day.** Mitigated, not eliminated (see §4).
- **Self-improvement poisoning.** A determined attacker who can plant content the agent later learns
  from could subtly bias future outputs; scope-isolation limits but does not eliminate this.
- **Physical/host compromise.** If the host itself is compromised, all guarantees fall - this is
  outside the platform's control surface.

---

_STRIDE: Spoofing, Tampering, Repudiation, Information disclosure, Denial of service, Elevation of
privilege. This is a living document - update it when trust boundaries, the deployment model, or the
governance controls change._
