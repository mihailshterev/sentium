# Final-State Overview

> Counterpart to [initial-concept-proposal.md](initial-concept-proposal.md), written now that
> development is complete. It records the system **as actually built** and how it diverged from the
> original proposal. Sequence/flow diagrams for each capability live in
> [new-diagrams/](new-diagrams/).

### Core Idea

- Self-hosted, privacy-preserving **AI agent platform** for the home/local network
- Runs **entirely within the local network** with **local model inference** - no external cloud
- Acts as a governed "Home Sentinel / Home Worker": reasoning, automation, and assistance under
  enforced permissions and full auditability
- The original "home data analytics" framing grew into a general, **trustworthy local-AI platform**

### Motivation

- Cloud-based AI and smart-home systems introduce privacy, data-ownership, connectivity, and
  transparency concerns
- Autonomous AI adds a second problem: it must be **trustworthy** - every action accountable,
  bounded, and reversible
- Goal: show that **capable, self-improving AI agents can run locally**, governed by an explicit
  policy layer and isolated execution, without sacrificing modularity or reliability

### Architectural Approach

- **Microservices architecture** - seven independent, loosely-coupled .NET 10 services
- Orchestrated locally by **.NET Aspire** (single entry point starts every service + its
  infrastructure)
- **Backend-for-Frontend (BFF)** security: the browser holds only an httpOnly cookie; the Gateway
  is the sole OIDC client and never exposes tokens to the frontend
- **Per-user data isolation** by default (global EF Core query filters), with explicit
  `Sovereign`/system bypass for background work
- **Async messaging** over NATS JetStream (workflow execution, real-time streaming, cache
  invalidation); **two-tier HybridCache** (in-process + Redis) kept consistent via broadcast
- Resilience (retry + timeout + circuit-breaker) and structured error handling applied globally

### Core System Components

The five originally-proposed services were re-shaped into seven built services. Mapping old → new:

- **AgentRuntime** - the core AI execution engine (agents, conversations, RAG, multi-agent
  orchestration, scheduling). Absorbs the old _AI Agent Service_ and _Analytics Service_.
- **Gateway** - YARP BFF reverse proxy; OIDC login/logout/token-refresh. The old _API Gateway / UI_
  entry point.
- **Identity** - OpenIddict OIDC/OAuth2 authorization server (was folded into "authentication" in
  the proposal).
- **Registry** - centralized runtime configuration (no equivalent in the proposal; emerged from the
  need for live, per-user/global settings).
- **Sandbox** - isolated code execution in hardened Docker containers (new; enables safe agent
  autonomy).
- **Sentinel** - Policy Decision Point; every user-facing action is checked via
  `POST /policy/evaluate` (new governance layer, the proposal's "strict permissions" made concrete).
- **Watchdog** - system health monitoring; the read-only successor to the _Inventory & Asset
  Service_, now focused on service uptime rather than household devices.
- **Frontends** - `sentium-portal` (React) and `sentium-identity-ui` (login/consent).
- **Infrastructure** (all local, started by Aspire): SQL Server (five isolated databases), Redis,
  NATS JetStream, Qdrant, Ollama, Seq, Azurite.

### AI Integration

- Local inference only, via **Ollama** behind the `Microsoft.Extensions.AI` abstraction
  (Gemma default, Qwen alternative) - no direct provider calls
- **RAG** on a **Qdrant** vector store with Nomic-Embed-Text (768-dim) across three collections:
  knowledge base, learnings, and user memories
- **Self-improvement loop**: `LearningAugmentedChatClient` auto-injects relevant past learnings;
  prompt enhancement is optional/configurable
- **Multi-agent orchestration** and **autonomous scheduling** for background, Sovereign-driven work
- Agents operate under strict, per-action permissions enforced by Sentinel and execute untrusted
  code only inside the Sandbox

### Example Functionalities

- Conversational AI assistant with live token/thought/tool streaming
- Multi-agent workflows and orchestration over domain tasks
- RAG-backed knowledge base with private + shared scopes
- Sandboxed code execution in hardened containers
- Policy-governed actions with a full audit trail (Sentinel)
- Scheduled, autonomous background tasks
- Service health monitoring and incident tracking (Watchdog)
- Semantic-map visualization of stored knowledge/memories

### Research Focus

- Privacy-preserving system architecture with local-only AI
- **Governed autonomy**: feasibility of a Policy Decision Point gating every AI action
- Feasibility of **self-improving** local agents under home-scale resource constraints
- Trade-offs between local and cloud-based AI
- Reliability and availability of a decentralized microservices system at home scale

### One-Line Summary

- _A self-hosted, privacy-first AI agent platform: local LLMs, RAG, and self-improving multi-agent
  orchestration - every action policy-governed and sandboxed - running entirely without cloud
  dependence._

---

### Evolution since the initial proposal

- **Scope shifted** from _home-network data analytics_ (device presence, traffic summaries, usage
  trends) to a **general local AI agent platform**. Passive household monitoring narrowed to the
  Watchdog service; the intelligence moved to the foreground.
- **Two concerns were added that the proposal only gestured at**: explicit **governance** (Sentinel
  PDP, audit logging) and **isolated execution** (Sandbox) - the machinery that makes "AI agents
  under strict permissions" real rather than aspirational.
- **Two services were introduced** that did not exist in the original plan: **Registry** (live
  configuration) and **Identity** as a standalone OIDC server, reflecting lessons learned about
  configuration drift and centralized auth.
- **The privacy-first, microservices, local-AI thesis held** - the core bet of the proposal proved
  workable; what changed was the surface area and the rigor around trust.
