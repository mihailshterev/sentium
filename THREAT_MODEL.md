# Sentium - Threat Model

> Security threat model for the Sentium self-hosted AI agent platform. Uses the
> [STRIDE](https://en.wikipedia.org/wiki/STRIDE_model) methodology over the system's trust
> boundaries. This document records the threats considered, the mitigations actually built into
> the system, and the residual risks accepted under the project's deployment model.
>
> Companion document: [docs/final-state-overview.md](docs/final-state-overview.md) (architecture
> as built).

---

## 1. Scope & Deployment Model

Sentium is a **self-hosted, privacy-first AI agent platform** intended to run **entirely within a
single trusted host or private network**, operated by one administrator (`Sovereign`). All AI
inference is local (llama.cpp's `llama-server`); there is no external cloud dependency.

Sentium runs as two .NET processes:

- **Sentium.Web** - a modular monolith hosting every module (Identity, Registry, Sentinel, Codex,
  Locus, Tempus, Praxis, Vigil) in one process. It is the **only publicly reachable entry point**: it serves the
  built portal, the `/api/{module}` API and the session endpoints from one HTTPS origin. During
  development the Vite dev server proxies `/api` to it, so the browser still sees a single origin.
- **Sentium.Sandbox** - a small, stateless process that runs agent-written code in throwaway
  Docker containers. It is kept out of Sentium.Web on purpose because it owns the Docker socket.
  It is reachable only on the internal network.

The infrastructure (PostgreSQL with pgvector, Redis, the llama-server inference servers, Azurite) lives on the same
**trusted internal network** and is not meant to be exposed to untrusted clients.

This deployment model is central to the threat model:

- Sentium.Web runs as a **single instance**. In-memory queues, the stream relay and settings cache
  eviction all assume one process.
- The trust model assumes **no untrusted tenant** sharing the internal network. Multi-tenant
  hostile-network deployment is explicitly out of scope (see §8 Residual Risks).

### Primary assets

| Asset                                                                   | Why it matters                                                             |
| ----------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| User credentials & the session cookie (`Sentium.Auth`)                  | Account takeover                                                           |
| ASP.NET Core Data Protection key ring                                   | Signs and encrypts the session cookie; whoever holds it can forge sessions |
| Per-user data (conversations, RAG memories, knowledge base, workspaces) | Privacy - the platform's core promise                                      |
| `X-Internal-Token` pre-shared key                                       | Reaches the Sandbox's `/execute` and Sentinel's policy-evaluation endpoint |
| PostgreSQL credentials                                                  | One login reads and writes every module's schema                           |
| Sentinel audit log                                                      | Accountability / forensic integrity                                        |
| The host running the Sandbox                                            | Container escape or Docker-socket abuse → full host compromise             |
| Runtime configuration (Registry)                                        | Disabling governance (autonomy level, policy toggles)                      |
| LLM models & inference                                                  | Resource exhaustion; manipulated outputs                                   |

### Actors

- **Anonymous / external attacker** - reaches only Sentium.Web's public surface: the portal's
  static files, login and registration, the `/health` and `/alive` endpoints, and Sentinel's
  token-gated policy-evaluation endpoint. Registration is open: anyone who can reach the server
  can create a `Member` account.
- **Authenticated `Member`** - standard user; their actions are user-scoped and policy-governed.
- **`Sovereign`** - administrator; trusted, can read audit, change global config, bypass data
  filters. The role can only be granted by an existing Sovereign.
- **The AI agent itself** - a _semi-trusted, potentially-compromised_ actor. Prompt injection,
  hallucination, or a malicious skill can make an agent attempt unauthorized actions. Much of the
  governance machinery (Sentinel, Sandbox, tool approvals) exists to bound this actor.
- **Malicious/buggy code** submitted for Sandbox execution.

---

## 2. Trust Boundaries

```
 [ Browser ]  --- HttpOnly + Secure + SameSite=Strict session cookie, never tokens
      |  (public, HTTPS)
 ===== TB1: public edge ==============================================================
      v
 [ Sentium.Web ]  portal · /api/{module} · session endpoints
    Identity · Registry · Sentinel · Codex · Locus · Tempus · Praxis · Vigil   (one process, in-process calls)
      |                                              ^
      |  POST /execute                               |  POST /api/sentinel/policy/evaluate
      |  (X-Internal-Token)                          |  (X-Internal-Token)
 ===== TB2: cross-process calls ======================|================================
      v                                              |
 [ Sentium.Sandbox ] ---------------------------------+
      |  (host Docker socket)
 ===== TB3: untrusted code execution =================================================
      v
 [ Ephemeral hardened Docker container ]

 Internal network: PostgreSQL (pgvector) · Redis · llama-server (llm, embeddings) · Azurite
```

- **TB1 - Public edge.** The only boundary exposed to untrusted input. Sentium.Web authenticates
  the session cookie, and every controller action must declare its own rule (`[Authorize]`,
  `[AuthorizeSovereign]`, `[AuthorizeSystem]` or an explicit `[AllowAnonymous]`). There is no
  gateway and no fallback policy in front of the API, so `ControllerAuthorizationTests` fails the
  build when an action declares none. EF Core query filters then scope every user-owned row.
- **Inside Sentium.Web there is no security boundary between modules.** They share a process,
  memory, a DI container and one database login. Module boundaries (`Contracts`, enforced by
  `ModuleBoundaryTests`) are an architectural control, not a security one: a flaw in one module
  is a flaw in all of them.
- **TB2 - Cross-process calls.** Only Sentium.Web ↔ Sentium.Sandbox crosses a process boundary,
  and both directions require `X-Internal-Token` (the `SystemCaller` policy). The Sandbox is not
  routable from the browser. Sentinel's policy-evaluation endpoint, however, lives on Sentium.Web
  and so sits on the public origin: there the token is the only control, with no network
  isolation behind it.
- **TB3 - Untrusted code.** Agent-submitted code runs inside an ephemeral, network-disabled,
  capability-stripped container. The boundary here is the container/kernel.

---

## 3. STRIDE Analysis by Boundary

### 3.1 Spoofing (identity)

| Threat                                         | Mitigation                                                                                                                                                                                                                                        |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Attacker steals the session from the browser   | ASP.NET Core Identity issues an `HttpOnly`, `Secure` (always), `SameSite=Strict` cookie (`Sentium.Auth`, 7-day sliding). There are no tokens in the browser, so XSS cannot exfiltrate a bearer credential.                                        |
| Cross-site request forgery                     | `SameSite=Strict` keeps the cookie off every cross-site request, and the portal is served from the same origin as the API. There are no separate antiforgery tokens.                                                                              |
| Credential stuffing / brute force on login     | Login and registration share a fixed-window rate limiter (`auth`, 10 requests per minute), and failed logins count towards account lockout (`lockoutOnFailure`). Unauthenticated API calls get `401`/`403`, never a redirect.                     |
| Caller impersonates the Sandbox or Sentium.Web | `X-Internal-Token` is compared in constant time (`InternalApiKeyAuthenticationHandler`, SHA-256 + `FixedTimeEquals`) and maps to the `SystemCaller` policy. Aspire generates the key per install; for Compose the operator supplies it in `.env`. |
| Agent claims to be a system caller             | Agents run in-process and cannot set request headers. The token is attached only by `InternalApiKeyDelegatingHandler` on the Praxis → Sandbox client and the Sandbox → Sentinel client.                                                           |

### 3.2 Tampering (integrity)

| Threat                                             | Mitigation                                                                                                                                                                                               |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Tampering with the session cookie                  | The cookie is **encrypted + signed** by ASP.NET Core Data Protection in Sentium.Web; it cannot be modified without the key ring.                                                                         |
| Modifying another user's data                      | **EF Core global query filters** scope every user-owned entity by `UserId`; cache keys and vector searches are user-scoped.                                                                              |
| Writing into another user's knowledge base         | Modules ingest in-process through `IDocumentIngestionService`. The Codex ingestion REST endpoints, whose request body names the target scope, are `Sovereign`-only.                                      |
| Tampering with runtime config to weaken governance | Global settings (`pdp`, `inference`, `vigil`) can be read and changed only by a `Sovereign`; per-user settings (`harness`) are self-scoped. Config is the Sovereign's responsibility (trusted actor).    |
| Tampering with the audit log                       | Audit records are **append-only, written by the policy engine**; read access is `Sovereign`-only. The table shares a database login with every other module, so this is not tamper-proof (see §7).       |
| Man-in-the-middle on Web ↔ Sandbox calls           | Mitigated by **network isolation**, with the internal token as an additional layer. Under Compose the two talk plain HTTP on the internal network (`http://sentium-web:8080`). mTLS is the upgrade path. |

### 3.3 Repudiation (accountability)

| Threat                                 | Mitigation                                                                                                                                                                                          |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Agent/user denies performing an action | **Sentinel writes a forensic audit record for every policy evaluation**, in-process or over HTTP (allow/deny, risk, alignment verdict, duration, `auditId`).                                        |
| Sandbox execution disowned             | The Sandbox is stateless; Praxis records every execution it requests, including policy denials (code, agent, prompt, exit code, artifacts, originating Sentinel `auditId`), in the `praxis` schema. |
| No centralized visibility              | Structured logs, traces and GenAI spans are exported over **OpenTelemetry (OTLP)** to the Aspire dashboard.                                                                                         |

### 3.4 Information Disclosure (confidentiality)

| Threat                                 | Mitigation                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Cross-user data leakage                | Per-user isolation via query filters; the **`Sovereign` and background bypass is explicit** (the Sovereign role, or `SystemScopeContext` activated by a background worker's DI scope), never accidental.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| Credentials leaked to the frontend     | The browser only ever holds the HttpOnly session cookie; there is no token for JavaScript to read.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| Direct blob-storage access             | Artifacts are **proxied through the backend** (`/api/sandbox/artifacts/*`); the browser never gets direct blob-store URIs.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| Data exfiltration by executed code     | Sandbox containers run with **`NetworkDisabled`** - no outbound network at all.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| RAG/memory leakage across scopes       | Knowledge chunks carry a **private (user) or shared scope**; retrieval is filtered by the acting user.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| Sensitive data sent to a cloud LLM     | **Local inference only** (llama-server); no provider calls leave the host.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| Household inventory exposure           | Locus is **shared by design**: every signed-in household member sees and edits all locations, assets and attachments (receipts may hold personal data). Assistants only see assets marked as shared (`IInventoryLookup` filters every query); location access notes and descriptions, and all attachments, never reach agents or the knowledge base. Shared assets are ingested with the **shared** knowledge scope.                                                                                                                                                                                                                                                                                                                                         |
| Personal timeline exposure             | Tempus timelines are **private, with no Sovereign bypass**: its query filter only skips for system scopes, so an administrator's own timeline never shows other members' tasks. The agent facade (`ITimelineAgentAccess`) runs in a system scope and therefore filters and stamps the **explicit acting user** on every call. Agents can read, add notes and **propose** tasks or moves - nothing changes until the user accepts (no approval-gated tools, so unattended runs can only suggest). Task titles, descriptions and notes reach models fenced as data; tool calls are classified as Sentinel resource `Timeline` with ids/dates (never titles) as resource ids; agents are capped at 50 pending proposals per user and 50 notes per user per day. |
| Attachments served from the app origin | Uploads are typed from their **magic bytes** (images, PDF, plain text only; SVG/HTML rejected) and served with `X-Content-Type-Options: nosniff` and `Content-Security-Policy: default-src 'none'; img-src 'self'; sandbox`; only images render inline, everything else is forced to download. File names never reach blob names or metadata.                                                                                                                                                                                                                                                                                                                                                                                                                |
| Operational detail exposed             | `/health` on Sentium.Web is anonymous and returns each check's name and exception message. OpenAPI and Scalar are mapped only in Development, which is also the environment Compose runs (see §7).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |

### 3.5 Denial of Service (availability)

| Threat                                           | Mitigation                                                                                                                                                                                                                                          |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Compromised agent floods actions                 | **`RateLimitingPolicy`** (sliding window per agent) limits the "blast radius" of a runaway agent.                                                                                                                                                   |
| Runaway / malicious code consumes host resources | Sandbox containers have **hard CPU & memory limits, `PidsLimit`, `nofile`/`nproc` ulimits, output size caps, and an execution timeout** (killed on overrun).                                                                                        |
| LLM judge hangs the policy pipeline              | `SemanticIntentPolicy` timeouts/errors return **`INCONCLUSIVE`** rather than blocking - availability preserved without silently allowing misaligned actions at low autonomy.                                                                        |
| One module exhausts the shared process           | Everything runs in one process, so a crash or resource leak in any module takes every module down. Workflow runs are bounded (concurrency limit, per-run timeout); queued runs lost on restart are recorded as cancelled. Vigil probes each module. |
| Login lockout of legitimate users                | The `auth` rate-limit window is shared by all clients, so an attacker can exhaust it and block legitimate logins for the rest of the minute (see §7).                                                                                               |

### 3.6 Elevation of Privilege

| Threat                                                | Mitigation                                                                                                                                                                                                                         |
| ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Member` performs `Sovereign`-only actions            | Two-tier role model enforced at the API; user/role management, model load/unload, global settings, audit and Codex ingestion are `Sovereign`-gated. Only a Sovereign can grant the role, and the last Sovereign cannot be removed. |
| A new endpoint ships without authorization            | No gateway enforces "authenticated by default" any more; `ControllerAuthorizationTests` requires every controller action to declare an access rule.                                                                                |
| Browser reaches privileged internal endpoints         | The Sandbox's `/execute` is not routable from the browser. Sentinel's `/api/sentinel/policy/evaluate` is, but it requires `X-Internal-Token` (see §7 for the Development fail-open).                                               |
| Agent escalates beyond user intent (prompt injection) | **`SemanticIntentPolicy`** compares the declared action against the original user prompt via a local LLM (temp 0). `MISALIGNED` → deny-with-alert. Behavior tightens at low `AutonomyLevel`.                                       |
| Agent tool call skips the PDP                         | Every Sentium tool carries a Sentinel classification and passes `SentinelFunctionMiddleware`, after human approval. Only the framework's skill functions (which load skill instructions) carry none and pass straight through.     |
| **Container escape → host compromise**                | Defence-in-depth hardening (see §4).                                                                                                                                                                                               |
| Policy bypass via unhandled exception                 | Sentinel is **fail-closed**: any unhandled error in any policy → deny with critical risk. Both callers also fail closed when the PDP is unreachable (Praxis' `SentinelClient`, the Sandbox's `HttpSentinelGateway`).               |

---

## 4. Sandbox: Untrusted Code Execution (highest-risk boundary)

Executing agent-generated code is the single highest-risk capability. Two-phase: **Sentinel must
authorize before any container starts** (the Sandbox calls Sentium.Web's policy endpoint with the
internal token and denies when it cannot reach it), then `DockerSandboxRunner` runs the code under
aggressive constraints set by `ContainerConfigBuilder`:

| Control                 | Setting / effect                                                                        |
| ----------------------- | --------------------------------------------------------------------------------------- |
| Network                 | `NetworkDisabled` - no network access at all                                            |
| Filesystem              | `ReadonlyRootfs`; `/tmp` is `tmpfs` with `noexec,nosuid,nodev`                          |
| User                    | `nobody` (unprivileged)                                                                 |
| Linux capabilities      | `CapDrop: ALL`                                                                          |
| Privileges              | `no-new-privileges:true`; Docker's default seccomp profile, or `Sandbox:SeccompProfile` |
| CPU / memory            | hard limits (`NanoCPUs`, `Memory` = `MemorySwap`, i.e. swap disabled)                   |
| Processes / descriptors | `PidsLimit`, `nofile` / `nproc` ulimits                                                 |
| Output / time           | bounded stdout/stderr size; execution timeout (container killed on overrun)             |
| Lifecycle               | container + working directory cleaned up guaranteed (`finally`)                         |

The high-risk tools (`execute_code_sandbox`, `schedule_recurring_task`) also require explicit user
approval in the assistant; unattended workflow runs do not offer them at all.

**The Docker socket.** The Sandbox mounts the host's Docker socket so it can start sibling
containers. Whoever controls the Sandbox process therefore controls the host's Docker daemon,
which is root-equivalent. This is why the Sandbox is a separate, stateless process with one
token-authenticated endpoint and no database.

**Residual risk:** a kernel/Docker 0-day enabling container escape would compromise the host. This
is inherent to running untrusted code on shared kernel containers; mitigated by hardening,
local-only deployment, and keeping the Docker host patched. A VM/gVisor/Firecracker isolation layer
is the stronger upgrade path.

---

## 5. AI-Specific Threats

The agent is treated as a **semi-trusted actor**. AI-specific risks beyond classic STRIDE:

| Threat                                                                                                              | Mitigation / status                                                                                                                                                                                                                                                                |
| ------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Prompt injection** (via documents, RAG content, tool output, user input) steering the agent to unintended actions | `SemanticIntentPolicy` alignment check; Sentinel gates every tool call & code execution; high-risk tools also require explicit user approval, and only the user who owns a paused run can approve it.                                                                              |
| **Prompt injection via household data**                                                                             | Asset notes for assistants are written by any household member and reach every user's agents. Tool output and knowledge-base text label them as household data, not instructions, and fence them; the inventory tools are read-only, so injected text cannot change the inventory. |
| **Excessive autonomy**                                                                                              | Configurable `AutonomyLevel`; at very high autonomy the alignment check is skipped (an accepted, operator-chosen trade-off), at low autonomy uncertain actions are denied.                                                                                                         |
| **Hallucinated / unsafe actions**                                                                                   | Same PDP gating; network-isolated, reversible sandboxed execution.                                                                                                                                                                                                                 |
| **Data poisoning of the self-improvement loop**                                                                     | Auto-injected learnings (`LearningsContextProvider`) are user-scoped; a learning only becomes global after `LearningSanitizationPipeline` approves it. _Note: poisoned learnings could degrade future responses._                                                                  |
| **Model/prompt extraction**                                                                                         | Limited exposure - local models, no external API; system prompts are server-side.                                                                                                                                                                                                  |
| **Resource exhaustion via inference**                                                                               | Rate limiting + per-action governance; a cap on tool-calling iterations per turn; local resource limits.                                                                                                                                                                           |

---

## 6. Infrastructure & Dependencies

| Component                           | Threat                               | Note                                                                                                                                                                                                                                                                                                                               |
| ----------------------------------- | ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| PostgreSQL (`sentiumdb`, pgvector)  | Over-broad access across modules     | One database, one schema per module. Every module connects with the same login (the `postgres` superuser under Compose), so schema separation is not a permission boundary.                                                                                                                                                        |
| Redis (HybridCache L2)              | Cache poisoning / stale data         | Internal network only. A settings change evicts Registry's cache entry directly; this assumes one Sentium.Web instance.                                                                                                                                                                                                            |
| llama-server / Azurite              | Direct access bypassing app controls | Internal network only. The inference servers require a bearer API key (`LLM_API_KEY`, generated by Aspire) on every request except `/health`, have their web UI disabled, and Compose binds their ports to `127.0.0.1`. The Compose file still publishes PostgreSQL, Redis and Azurite on all interfaces for convenience (see §7). |
| Docker daemon (via the Sandbox)     | Root-equivalent control of the host  | See §4. Only the Sandbox mounts the socket.                                                                                                                                                                                                                                                                                        |
| Third-party packages (NuGet / pnpm) | Supply-chain compromise              | Pinned lockfiles; keep dependencies patched (see §7).                                                                                                                                                                                                                                                                              |
| Secrets in source/containers        | Leakage of keys/connection strings   | Use `.env` / Aspire secrets (`.env.example` is the template); **never commit real secrets**. `.gitignore` excludes env files.                                                                                                                                                                                                      |

---

## 7. Recommendations / Hardening Backlog

These are not yet fully addressed and are worth tracking:

1. **Fail closed when the internal key is missing.** In Development, an empty `InternalApi:ApiKey`
   makes the handler accept every caller. Compose runs Sentium.Web in Development, so a blank
   `INTERNAL_API_KEY` opens Sentinel's public policy-evaluation endpoint to anyone.
2. **Trim `/health` on the public origin.** Only the Sandbox's `/health` needs to be probed over
   HTTP; Sentium.Web's modules are probed in-process. Its anonymous, detailed JSON report (check
   names and exception messages) should be restricted or reduced to a status.
3. **Run Compose outside Development.** Today Compose needs Development for startup migrations;
   it also exposes OpenAPI and Scalar.
4. **Partition the login rate limiter** per client instead of one window shared by everyone.
5. **Bind or drop the Compose infrastructure ports** (PostgreSQL, Redis, Azurite; the inference ports are already bound to `127.0.0.1`) so they
   are not reachable from other machines.
6. **Per-module database roles** with grants on their own schema, and a non-superuser login.
7. **Persist and protect the Data Protection key ring** (today framework defaults: recreating the
   container signs everyone out, and keys are not encrypted at rest).
8. **Dependency scanning** in CI (Dependabot / `dotnet list package --vulnerable`, `pnpm audit`).
9. **Stronger sandbox isolation** (gVisor / microVM) if untrusted code volume grows.
10. **mTLS between Sentium.Web and the Sandbox** if Sentium is ever deployed on a network with
    untrusted peers.
11. **Security headers / CSP** review on Sentium.Web, which serves the portal, to harden against XSS.
12. **Audit-log tamper-evidence** (e.g. hash chaining) if forensic guarantees must survive a
    compromised host.
13. **Provenance/validation for RAG-ingested content** to reduce learning-loop poisoning.
14. **Secret rotation** procedure for the `X-Internal-Token`, the PostgreSQL password, the TLS
    certificate and the Data Protection keys.
15. **A documented first-Sovereign bootstrap**: new accounts are Members, and only an existing
    Sovereign can grant the role.

---

## 8. Residual / Accepted Risks

Accepted under the self-hosted, single-operator, trusted-network deployment model:

- **Pre-shared internal token.** A `X-Internal-Token` key (not mTLS) protects the two cross-process
  endpoints. For the Sandbox it is _defence-in-depth on top of_ network isolation; for Sentinel's
  policy-evaluation endpoint on the public origin it is the only control. Inadequate for a
  hostile/multi-tenant network (upgrade path: mTLS, or moving that endpoint off the public origin).
- **One process, one database login.** A flaw in any module exposes the data of all of them.
- **Trusted `Sovereign`.** The administrator can read all audit, change governance config, and
  bypass data isolation. There is no protection against a malicious Sovereign - they own the host.
- **Container escape via kernel 0-day, or abuse of the Docker socket.** Mitigated, not eliminated
  (see §4).
- **Self-improvement poisoning.** A determined attacker who can plant content the agent later learns
  from could subtly bias future outputs; scope-isolation limits but does not eliminate this.
- **Physical/host compromise.** If the host itself is compromised, all guarantees fall - this is
  outside the platform's control surface.

---

_STRIDE: Spoofing, Tampering, Repudiation, Information disclosure, Denial of service, Elevation of
privilege. This is a living document - update it when trust boundaries, the deployment model, or the
governance controls change._
