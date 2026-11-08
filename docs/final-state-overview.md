# Final-State Overview

> Counterpart to [initial-concept-proposal.md](initial-concept-proposal.md), written now that
> development is complete. It records the system **as actually built** and how it diverged from the
> original proposal. Sequence/flow diagrams for each capability live in
> [diagrams/](diagrams/).

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

- **Modular monolith** - one .NET 10 host (**Sentium.Web**) with six modules, plus one separate,
  stateless process (**Sentium.Sandbox**) that isolates code execution because it owns the Docker
  socket
- Modules reach each other **only through their `Contracts`** (interfaces + DTOs), in-process; a
  unit test fails the build when one module uses another module's internals
- Orchestrated locally by **.NET Aspire** (single entry point starts Sentium.Web, the Sandbox, the
  portal and the infrastructure); a Docker Compose file covers self-hosted deployment
- **Cookie-only authentication** with ASP.NET Core Identity: the browser holds an HttpOnly session
  cookie and the portal is served from the same origin as the API, so there are no tokens, no
  gateway and no CORS
- **Per-user data isolation** by default (global EF Core query filters), with explicit
  `Sovereign`/system bypass for background work
- **In-process background work** - no message broker: workflow runs and file ingestion go through
  in-memory queues drained by hosted services, and agent output streams fan out through an
  in-memory relay that replays missed updates on reconnect
- **Two-tier HybridCache** (in-process + Redis); Registry owns the settings cache and evicts its
  entry on change, so consumers never hold stale copies
- Resilience (retry + timeout + circuit-breaker) and structured error handling applied globally

### Core System Components

The five originally-proposed services became six modules in one host plus the isolated Sandbox.
Mapping old → new:

- **Praxis** - the core AI execution engine (agents, the streaming assistant, conversations,
  multi-agent orchestration, workflows, learnings, skills, tools, workspaces, scheduling, and the
  sandbox execution history). Absorbs the old _AI Agent Service_ and _Analytics Service_.
- **Codex** - the knowledge base: embeddings, the pgvector store, document ingestion and the
  knowledge map. Other modules push their content (learnings, memories, workspace files) through it.
- **Identity** - ASP.NET Core Identity: users, roles, profile, registration and the session
  endpoints the portal uses (was folded into "authentication" in the proposal).
- **Registry** - centralized runtime configuration (no equivalent in the proposal; emerged from the
  need for live, per-user/global settings).
- **Sentinel** - Policy Decision Point; every agent tool call and every sandbox execution is
  checked before it runs (new governance layer, the proposal's "strict permissions" made concrete).
- **Locus** - the household inventory: a location tree, assets (tags, search, attachments) and
  the read-only view assistants get of them. The successor to the proposal's _Inventory & Asset
  Service_; items marked as shared reach agents through the knowledge base and inventory tools.
- **Tempus** - each user's private year timeline: tasks on days or pinned to months, notes, and the
  tasks and moves agents propose for the user to accept. Agents reach it through timeline tools and the
  `TimelinePlanner` agent; the portal draws it as a glowing path through the months.
- **Vigil** - system health monitoring, focused on module and dependency health.
- **Sentium.Sandbox** - isolated code execution in hardened Docker containers (new; enables safe
  agent autonomy). Stateless: it authorizes each run with Sentinel and returns the result, and
  Praxis records it.
- **Frontend** - `sentium-portal` (React), including its own login page, built into and served by
  Sentium.Web.
- **Infrastructure** (all local, started by Aspire): PostgreSQL with pgvector (one database, one
  schema per module), Redis, llama.cpp's `llama-server` (a chat-model router plus an embeddings server), Azurite.

### AI Integration

- Local inference only, via **llama-server**'s OpenAI-compatible API behind the `Microsoft.Extensions.AI` abstraction, with agents
  and multi-agent workflows built on the **Microsoft Agent Framework** (Gemma default, Qwen
  alternative) - no direct provider calls
- **RAG** on **PostgreSQL + pgvector** with `embeddinggemma-2:740m` (768-dim) across three
  collections: knowledge base, learnings, and user memories
- **Self-improvement loop**: a context provider recalls relevant past learnings and adds them to the
  agent's instructions on every turn; prompt enhancement is optional/configurable
- **Multi-agent orchestration** as a plan → step → check graph tuned for small local models, and
  **autonomous scheduling** for background, Sovereign-driven work
- Agents operate under strict, per-action permissions enforced by Sentinel and execute untrusted
  code only inside the Sandbox

### Example Functionalities

- Conversational AI assistant with live token/thought/tool streaming
- Multi-agent workflows and orchestration over domain tasks
- RAG-backed knowledge base with private + shared scopes
- Sandboxed code execution in hardened containers
- Policy-governed actions with a full audit trail (Sentinel)
- Scheduled, autonomous background tasks
- Household inventory that assistants can search ("where is the drill?") (Locus)
- A year timeline that agents help plan: workload review, suggested tasks and moves, progress notes (Tempus)
- Module and dependency health monitoring and incident tracking (Vigil)
- Semantic-map visualization of stored knowledge/memories

### Research Focus

- Privacy-preserving system architecture with local-only AI
- **Governed autonomy**: feasibility of a Policy Decision Point gating every AI action
- Feasibility of **self-improving** local agents under home-scale resource constraints
- Trade-offs between local and cloud-based AI
- How much distribution a single-operator, home-scale system actually needs - explored by building
  it as microservices first and consolidating it into a modular monolith

### One-Line Summary

- _A self-hosted, privacy-first AI agent platform: local LLMs, RAG, and self-improving multi-agent
  orchestration - every action policy-governed and sandboxed - running entirely without cloud
  dependence._

---

### Evolution since the initial proposal

- **Scope shifted** from _home-network data analytics_ (device presence, traffic summaries, usage
  trends) to a **general local AI agent platform**. Passive household monitoring narrowed to the
  Vigil module; the intelligence moved to the foreground.
- **Two concerns were added that the proposal only gestured at**: explicit **governance** (Sentinel
  PDP, audit logging) and **isolated execution** (Sandbox) - the machinery that makes "AI agents
  under strict permissions" real rather than aspirational.
- **Two components were introduced** that did not exist in the original plan: **Registry** (live
  configuration) and a dedicated **Identity** module, reflecting lessons learned about
  configuration drift and centralized auth.
- **From microservices to a modular monolith.** The system was first built as seven independent
  services behind a YARP gateway, with an OpenIddict authorization server, NATS JetStream
  messaging, five SQL Server databases and a Qdrant vector store. For a single operator on one
  host that distribution cost more than it bought: every feature crossed several network hops,
  auth needed token exchange and refresh, and keeping caches and streams consistent needed a
  broker. The services were consolidated into modules of one host with the same boundaries
  (enforced by tests instead of the network), cookie authentication, in-process queues, one
  PostgreSQL database and pgvector. Only the Sandbox stayed a separate process, because isolation
  there is a security requirement, not an organizational one.
- **The privacy-first, modular, local-AI thesis held** - the core bet of the proposal proved
  workable; what changed was the surface area, the deployment shape, and the rigor around trust.
