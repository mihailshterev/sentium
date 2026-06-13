# Installation guide

This guide covers every supported way to install and run **Sentium** - the whole platform (seven
micro-services, both front-ends, and all backing infrastructure):

- **[Method A - .NET Aspire](#method-a--net-aspire-recommended-for-development)** - the recommended
  path for **day-to-day development**. One command, a rich dashboard, hot reload, and no
  certificate/issuer setup.
- **[Method B - Docker Compose](#method-b--docker-compose-self-hosted-deployment)** - **self-hosted
  deployment** with plain Docker, without installing the .NET SDK or pnpm on the host.
- **[Method C - Front-end only](#method-c--front-end-only)** - run just a front-end against an
  already-running backend.

Both full-stack methods run the **same** services, wired with the same connection strings,
service-discovery keys, security model, and AI models. Their intentional differences are summarised
in [Aspire vs. Compose parity](#aspire-vs-compose-parity).

---

## Table of contents

- [Choosing a method](#choosing-a-method)
- [Method A - .NET Aspire](#method-a--net-aspire-recommended-for-development)
- [Method B - Docker Compose](#method-b--docker-compose-self-hosted-deployment)
- [Method C - Front-end only](#method-c--front-end-only)
- [GPU acceleration](#gpu-acceleration)
- [Day-two operations (Compose)](#day-two-operations-compose)
- [Troubleshooting](#troubleshooting)
- [Aspire vs. Compose parity](#aspire-vs-compose-parity)

---

## Choosing a method

|                    | **Method A - .NET Aspire**                             | **Method B - Docker Compose**                                 |
| ------------------ | ------------------------------------------------------ | ------------------------------------------------------------- |
| **Best for**       | Local development                                      | Self-hosted deployment                                        |
| **Host needs**     | .NET 10 SDK, pnpm, Docker                              | Docker only (+ .NET SDK once, for a dev cert)                 |
| **Services run**   | As host processes (hot reload)                         | As built container images                                     |
| **Front-ends**     | Vite dev servers (portal `:5173`, identity UI `:5174`) | Portal via nginx (`:5173`); identity UI bundled into Identity |
| **TLS / cookie**   | Handled by the Aspire HTTPS pipeline                   | Gateway terminates TLS with a dev cert you generate           |
| **Observability**  | Aspire dashboard                                       | Seq (`:8090`) + `docker compose logs`                         |
| **Model download** | Startup **blocks** until models are pulled             | Pulled in the background; first chat waits                    |
| **GPU**            | Requested by default                                   | Opt-in                                                        |

Both methods require **Docker** to be running: Aspire launches the infrastructure (and the code
sandbox) as containers, and Compose builds and runs everything in containers.

---

## Method A - .NET Aspire (recommended for development)

The [.NET Aspire AppHost](../src/aspire/Sentium.AppHost/Program.cs) is the single entry point for
local development. One command starts all seven services, both front-ends, and every backing
dependency (SQL Server, Redis, NATS, Qdrant, Ollama, Seq, Azurite) as managed containers - with a
live dashboard, hot reload, and automatic service discovery.

### Prerequisites

| Requirement                           | Notes                                                                                                                                                                                 |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **.NET 10 SDK**                       | Pinned to `10.0.102`+ by [`global.json`](../global.json). No separate "Aspire workload" is needed - the AppHost references the Aspire SDK via NuGet, so the .NET SDK alone is enough. |
| **pnpm 11**                           | The AppHost runs `pnpm install` + Vite for the two front-ends.                                                                                                                        |
| **Docker Desktop / Engine** (running) | Aspire starts the infrastructure containers, and the sandbox executes code via the host Docker daemon.                                                                                |
| **An NVIDIA GPU (optional)**          | The AppHost requests GPU support for Ollama by default. CPU-only works but is slow. See [GPU acceleration](#gpu-acceleration).                                                        |

### 1. Trust the local HTTPS dev certificate (one-time)

Aspire serves its dashboard and the HTTPS service endpoints with the ASP.NET Core developer
certificate:

```bash
dotnet dev-certs https --trust
```

### 2. Provide the secret parameters (first run)

The AppHost declares four **secret parameters** with no committed defaults: the SQL `sa` password,
the internal API key, and the two OIDC client secrets (`gateway-bff`, `service-worker`). Provide
them either way:

- **Interactively** - on first run, the Aspire dashboard prompts for any parameter that has no
  value, and persists what you enter to user secrets.
- **Pre-seeded** - set them once with user secrets (the AppHost has a `UserSecretsId`):

  ```bash
  cd src/aspire/Sentium.AppHost
  dotnet user-secrets set "Parameters:sql-password" "Your_Strong_Password123"
  dotnet user-secrets set "Parameters:internal-api-key" "<random-32-chars>"
  dotnet user-secrets set "Parameters:gateway-bff-secret" "<random>"
  dotnet user-secrets set "Parameters:service-worker-secret" "<random>"
  ```

  `sql-password` must satisfy SQL Server's complexity rules (≥ 8 chars mixing upper/lower case,
  digits, and symbols).

### 3. Run

```bash
dotnet run --project src/aspire/Sentium.AppHost/Sentium.AppHost.csproj
```

The **Aspire dashboard** URL (with a one-time login token) is printed in the console. Open it to
watch every resource start, stream logs, and follow the model downloads.

### What gets started

All seven services (`identity`, `registry`, `sentinel`, `sandbox`, `agent-runtime`, `watchdog`,
`gateway`), both front-ends (`sentium-portal`, `sentium-identity-ui`), and the infrastructure (SQL
Server with five databases, Redis, NATS JetStream, Qdrant, Ollama, Seq, Azurite). The default models
(`gemma4:e4b` + `nomic-embed-text`) are pulled into the Ollama volume; Aspire **blocks** the
dependent services until the pull completes.

### Endpoints

| What                          | URL                                     |
| ----------------------------- | --------------------------------------- |
| **Portal** (start here)       | http://localhost:5173                   |
| Identity UI (login / consent) | http://localhost:5174                   |
| Aspire dashboard              | printed on startup (with a login token) |

Every other service endpoint is discoverable from the dashboard.

> **First run takes a while** - Docker pulls the infrastructure images and Ollama downloads the
> multi-GB models. Subsequent starts reuse the persisted data volumes.

---

## Method B - Docker Compose (self-hosted deployment)

Self-host the entire platform with plain Docker Compose - every micro-service, both front-ends, and
all backing infrastructure - without installing the .NET SDK or pnpm on the host. This is the
deployment-oriented companion to the [`docker-compose.yml`](../docker-compose.yml) at the repository
root.

### Prerequisites

| Requirement                                      | Notes                                                                                                       |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| **Docker Engine 24+ with the Compose v2 plugin** | `docker compose version` should print v2.x. Docker Desktop (Windows/macOS) bundles it.                      |
| **.NET SDK 10**                                  | Needed **only** for the one-time TLS dev-certificate step. The services themselves build inside containers. |
| **~20 GB free disk**                             | For base images, the eight built images, the downloaded models, and the persisted data volumes.             |
| **An NVIDIA GPU (optional)**                     | Strongly recommended for usable local-LLM latency. See [GPU acceleration](#gpu-acceleration).               |

You do **not** need Node/pnpm or the .NET runtime on the host - the front-ends and services are
compiled inside multi-stage image builds.

### Quick start

```bash
# 1. Configure secrets and ports
cp .env.example .env            # PowerShell: Copy-Item .env.example .env
#    …then edit the secrets in .env

# 2. Generate the Gateway's TLS dev certificate (use the same password as CERT_PASSWORD in .env)
mkdir certs    # dotnet dev-certs does not create the target directory
dotnet dev-certs https -ep certs/sentium.pfx -p <CERT_PASSWORD>
dotnet dev-certs https --trust

# 3. Build and start everything
docker compose up --build
```

Then open the portal at **http://localhost:5173**.

> **First run takes a while.** Docker downloads base images, builds eight images, and pulls the chat +
> embedding models into Ollama. The platform UI and login come up quickly; the **first agent chat
> waits** until the models finish downloading (watch the `ollama-init` logs).

### Step-by-step

#### 1. Configure `.env`

Copy the template and edit it:

```bash
cp .env.example .env            # PowerShell: Copy-Item .env.example .env
```

At minimum, change every value under **Secrets** to a unique random string. Generate one with:

- **bash / macOS / Linux:** `openssl rand -base64 32`
- **PowerShell (Windows):** `[Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }))`

`SQL_PASSWORD` must additionally satisfy SQL Server's complexity rules (≥ 8 characters, mixing
upper/lower case, digits, and symbols). See the [Configuration reference](#configuration-reference)
for every variable.

#### 2. Generate the Gateway TLS certificate

The Gateway terminates HTTPS so the BFF auth cookie can be issued as `Secure` / `SameSite=None`
across the portal origin. Compose mounts a developer certificate from `./certs/sentium.pfx` at
runtime - generate it with the **same password** you put in `CERT_PASSWORD`:

```bash
mkdir certs    # PowerShell: New-Item -ItemType Directory certs
dotnet dev-certs https -ep certs/sentium.pfx -p <CERT_PASSWORD>
dotnet dev-certs https --trust
```

> `dotnet dev-certs` does **not** create the output directory, so create `certs/` first.
> If you skip it and run `docker compose up`, Docker creates an empty `certs/` mount and the
> Gateway fails to find the certificate.

`--trust` adds the certificate to your OS trust store so the browser accepts `https://localhost:8443`
without warnings. (On Linux, trusting dev certs is browser-specific - you may need to accept the
certificate manually the first time.)

The `certs/` directory is git-ignored and excluded from the Docker build context; the certificate is
mounted, never baked into an image.

#### 3. Build and start

```bash
docker compose up --build          # foreground, streams all logs
# or
docker compose up --build -d       # detached / background
```

On first start the services apply their EF Core migrations automatically (they run in the
`Development` environment for exactly this reason), and the one-shot `ollama-init` container pulls
the models defined by `SENTIUM_MODEL` and `SENTIUM_EMBEDDING_MODEL`, retrying until they succeed.

To stop:

```bash
docker compose down                # stop & remove containers, keep data volumes
```

### What gets started

#### Infrastructure

| Service       | Image                                        | Host port(s) | Purpose                                                                                                    |
| ------------- | -------------------------------------------- | ------------ | ---------------------------------------------------------------------------------------------------------- |
| `sql`         | `mcr.microsoft.com/mssql/server:2022-latest` | 1433         | Relational data - five databases (`identitydb`, `agentruntimedb`, `sentineldb`, `sandboxdb`, `registrydb`) |
| `redis`       | `redis:7-alpine`                             | 6379         | HybridCache L2                                                                                             |
| `nats`        | `nats:2-alpine`                              | 4222, 8222   | JetStream messaging + cache invalidation (8222 = monitoring)                                               |
| `qdrant`      | `qdrant/qdrant:latest`                       | 6333, 6334   | Vector store for RAG (6333 REST/dashboard, 6334 gRPC)                                                      |
| `azurite`     | `mcr.microsoft.com/azure-storage/azurite`    | 10000        | Azure Blob Storage emulator (workspace files & sandbox artifacts)                                          |
| `seq`         | `datalust/seq:latest`                        | 8090         | Structured log aggregation (OTLP ingest)                                                                   |
| `ollama`      | `ollama/ollama:0.30.5`                       | 11434        | Local LLM inference                                                                                        |
| `ollama-init` | `ollama/ollama:0.30.5`                       | -            | One-shot model puller (exits when models are ready)                                                        |

#### Application services

| Service         | Dockerfile                                 | Exposed via      | Role                                                           |
| --------------- | ------------------------------------------ | ---------------- | -------------------------------------------------------------- |
| `registry`      | `src/services/Registry/.../Dockerfile`     | internal         | Centralized runtime configuration                              |
| `identity`      | `src/services/Identity/.../Dockerfile`     | **8081**         | OIDC/OAuth2 server + login/consent UI (bundled into `wwwroot`) |
| `sentinel`      | `src/services/Sentinel/.../Dockerfile`     | internal         | Policy Decision Point (PDP)                                    |
| `sandbox`       | `src/services/Sandbox/.../Dockerfile`      | internal         | Hardened Docker code execution                                 |
| `agent-runtime` | `src/services/AgentRuntime/.../Dockerfile` | internal         | Core engine: agents, RAG, orchestration, scheduling            |
| `watchdog`      | `src/services/Watchdog/.../Dockerfile`     | internal         | Health monitoring                                              |
| `gateway`       | `src/services/Gateway/Dockerfile`          | **8443** (HTTPS) | YARP BFF - the only public API surface                         |
| `portal`        | `src/clients/sentium-portal/Dockerfile`    | **5173**         | React SPA (served by nginx)                                    |

The Identity login/consent SPA (`sentium-identity-ui`) is **not** a separate container under
Compose - it is built and bundled into the Identity service's `wwwroot`, so `/login` is served
same-origin from `http://localhost:8081`.

#### Endpoints once healthy

| What                           | URL                             |
| ------------------------------ | ------------------------------- |
| **Portal** (start here)        | http://localhost:5173           |
| Gateway (API / BFF)            | https://localhost:8443          |
| Identity (OIDC issuer + login) | http://localhost:8081           |
| Seq (logs)                     | http://localhost:8090           |
| Qdrant dashboard               | http://localhost:6333/dashboard |
| Ollama API                     | http://localhost:11434          |

### Verifying the installation

```bash
# All containers should be "running" (and healthy where a healthcheck is defined).
docker compose ps

# Watch the model download finish - the platform's first chat depends on this.
docker compose logs -f ollama-init      # ends with "All models ready."

# Confirm the back-end is reachable through the Gateway (expects an auth challenge, i.e. 401).
curl -k https://localhost:8443/bff/user
```

Then open **http://localhost:5173**, register or log in, and start a conversation. The Watchdog page
inside the portal shows a live health view of every service and infrastructure dependency.

### Configuration reference

All configuration lives in `.env` (see [`.env.example`](../.env.example)). Compose injects these
values plus the connection strings and service-discovery keys that mirror what Aspire provides at
runtime.

| Variable                  | Default                   | Description                                                                             |
| ------------------------- | ------------------------- | --------------------------------------------------------------------------------------- |
| `SQL_PASSWORD`            | `Your_Strong_Password123` | SQL Server `sa` password. Must meet SQL complexity rules.                               |
| `INTERNAL_API_KEY`        | `change-me-…`             | Pre-shared `X-Internal-Token` for service-to-service calls. Shared by all services.     |
| `GATEWAY_BFF_SECRET`      | `change-me-…`             | OIDC client secret for the `gateway-bff` client.                                        |
| `SERVICE_WORKER_SECRET`   | `change-me-…`             | OIDC client secret for the `service-worker` (client-credentials) client.                |
| `QDRANT_API_KEY`          | `change-me-…`             | API key the Qdrant server requires and the clients send. Any non-empty string.          |
| `CERT_PASSWORD`           | `change-me-…`             | Password of `certs/sentium.pfx`. Must match the `dotnet dev-certs` step.                |
| `GATEWAY_HTTPS_PORT`      | `8443`                    | Host HTTPS port for the Gateway / browser-facing API.                                   |
| `IDENTITY_HTTP_PORT`      | `8081`                    | Host HTTP port for the Identity issuer + login UI.                                      |
| `PORTAL_PORT`             | `5173`                    | Host HTTP port for the portal SPA.                                                      |
| `SENTIUM_MODEL`           | `gemma4:e4b`              | Chat/reasoning model (any valid Ollama tag).                                            |
| `SENTIUM_EMBEDDING_MODEL` | `nomic-embed-text`        | Embedding model. Must be 768-dimensional to match the RAG vector size.                  |
| `SANDBOX_JOBS_DIR`        | `/tmp/sentium-sandbox`    | Per-job sandbox working directory - identical on host and inside the Sandbox container. |
| `AZURITE_CONNECTION`      | _(well-known dev key)_    | Azurite blob connection string. Safe to leave as-is.                                    |

> **Changing ports?** If you change `GATEWAY_HTTPS_PORT`, `IDENTITY_HTTP_PORT`, or `PORTAL_PORT`,
> the OIDC redirect URIs, issuer, CORS origin, and the portal's baked-in API base are all derived
> from those variables - so a full `docker compose up --build` (to rebuild the portal with the new
> API base) keeps everything consistent.

### Platform notes

#### OIDC issuer & `host.docker.internal`

The Identity server is the canonical OIDC issuer and must be reachable under the **same URL** from
both your browser and the back-end containers. Compose uses `http://host.docker.internal:8081` for
this:

- **Docker Desktop (Windows/macOS):** works out of the box - `host.docker.internal` resolves to the
  host from inside containers, and the browser reaches the published `8081` port.
- **Native Linux:** the compose file maps `host.docker.internal` to the host gateway for the
  containers, but you must also let **your browser** resolve it. Add this line to `/etc/hosts`:

  ```
  127.0.0.1 host.docker.internal
  ```

#### Windows / WSL2

- Run Docker Desktop with the **WSL2 backend**.
- The code sandbox bind-mounts the host Docker socket and a job directory that must share an
  **identical path** on host and container (`SANDBOX_JOBS_DIR`). Point it at a path **inside your
  WSL2 distro** (the default `/tmp/sentium-sandbox` resolves correctly under the WSL2 backend).

#### macOS

Works with Docker Desktop out of the box. Apple-silicon machines run CPU inference only (no NVIDIA
GPU passthrough), so expect slower first responses.

#### Container security

The service images run **non-root** (`$APP_UID`, UID 1654) - except **Sandbox** (which drives the
host Docker socket) and **Gateway** (which reads the mounted TLS cert), which require root.

---

## Method C - Front-end only

If the backend is already running (via Method A or B), you can run a front-end on its own with the
Vite dev server - useful for UI work with hot reload:

```bash
cd src/clients/sentium-portal
pnpm install
pnpm dev            # Vite dev server on http://localhost:5173
```

The portal talks only to the Gateway BFF; point it at a running Gateway with the `VITE_API_BASE`
environment variable (defaults to the Aspire/Compose Gateway URL). The same pattern applies to
`src/clients/sentium-identity-ui`.

---

## GPU acceleration

Local inference is slow on CPU. To use an NVIDIA GPU you need the
[NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
installed on the host.

- **Aspire (Method A):** GPU support for Ollama is requested **by default** - no action needed when a
  GPU + toolkit are present.
- **Compose (Method B):** GPU is **opt-in** so the stack still starts on GPU-less hosts. Uncomment the
  `deploy:` block on the `ollama` service in [`docker-compose.yml`](../docker-compose.yml):

  ```yaml
  deploy:
    resources:
      reservations:
        devices:
          - driver: nvidia
            count: all
            capabilities: [gpu]
  ```

  Then `docker compose up -d ollama` to recreate the container with GPU access.

Both methods apply the same Ollama performance tuning (`OLLAMA_FLASH_ATTENTION`, `OLLAMA_NUM_PARALLEL`,
`OLLAMA_KEEP_ALIVE`).

---

## Day-two operations (Compose)

```bash
# Tail logs for one service (or all)
docker compose logs -f agent-runtime
docker compose logs -f

# Rebuild after pulling new code
docker compose up --build -d

# Stop containers, keep data
docker compose down

# Full reset - delete ALL persisted data (databases, vectors, blobs, downloaded models)
docker compose down -v

# Re-pull / change models without a full reset
#   1) edit SENTIUM_MODEL / SENTIUM_EMBEDDING_MODEL in .env
#   2) re-run the one-shot puller
docker compose up -d ollama-init
```

Persisted state lives in named volumes: `sql-data`, `redis-data`, `nats-data`, `qdrant-data`,
`azurite-data`, `seq-data`, `ollama-data`. They survive `docker compose down` and are only removed by
`down -v`.

---

## Troubleshooting

### Aspire (Method A)

**The dashboard or a service fails with a TLS / certificate error.**
Run `dotnet dev-certs https --trust` (then restart). On Linux you may need to trust the cert in your
browser manually.

**Startup hangs waiting on a resource / a service never goes green.**
Open the dashboard and check the resource's logs. The most common cause on first run is the Ollama
model download (multi-GB) - dependent services wait until it finishes.

**A service errors that a parameter has no value.**
Provide the four secret parameters via the dashboard prompt or `dotnet user-secrets` (see
[Method A, step 2](#2-provide-the-secret-parameters-first-run)).

### Docker Compose (Method B)

**The build fails during `pnpm install` with `ERR_PNPM_LOCKFILE_CONFIG_MISMATCH`.**
Make sure you are on a current checkout - the frontend Dockerfiles must copy each package's
`pnpm-workspace.yaml` (which carries the pnpm `overrides`) alongside `package.json` and
`pnpm-lock.yaml` before the frozen install. This is already handled in the committed Dockerfiles.

**Login redirects to a page that won't load / "can't reach host.docker.internal".**
Your browser can't resolve `host.docker.internal`. On native Linux add
`127.0.0.1 host.docker.internal` to `/etc/hosts` (see [Platform notes](#platform-notes)).

**Browser warns the certificate is not trusted on `https://localhost:8443`.**
Run `dotnet dev-certs https --trust` (re-run after regenerating the cert), or accept the certificate
manually. Confirm `CERT_PASSWORD` in `.env` matches the password used to create `certs/sentium.pfx`.

**Agent chat says the model isn't available / hangs on the first message.**
The models are still downloading. Watch `docker compose logs -f ollama-init` until it prints
`All models ready.` First-run downloads of multi-GB models can take a while.

**A service restarts repeatedly / can't connect to SQL.**
SQL Server can take 30–60 s to become healthy on first boot. Services wait for the SQL healthcheck,
but if it never goes healthy, check `docker compose logs sql` - the most common cause is a
`SQL_PASSWORD` that doesn't meet the complexity requirements.

**Sandbox code execution fails.**
Verify the host Docker socket is mounted and `SANDBOX_JOBS_DIR` resolves to the same absolute path on
host and container. On Windows, that path must live inside your WSL2 distro.

**Port already in use.**
Change the offending `*_PORT` value in `.env` (and rebuild so the portal picks up the new API base).

---

## Aspire vs. Compose parity

Both full-stack methods are functionally equivalent. The intentional differences are:

| Aspect            | Aspire AppHost                                       | Docker Compose                                                                                                                                     |
| ----------------- | ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| Identity login UI | Separate `sentium-identity-ui` dev server (`:5174`)  | Bundled into the Identity service `wwwroot`, served same-origin                                                                                    |
| TLS / auth cookie | Handled by the Aspire HTTPS dev pipeline             | Gateway terminates TLS using `certs/sentium.pfx` (one-time `dotnet dev-certs` step)                                                                |
| OIDC issuer URL   | Aspire service discovery                             | `http://host.docker.internal:8081` (reachable identically from browser and containers)                                                             |
| Model readiness   | Startup **blocks** until models download (`WaitFor`) | Services start immediately; `ollama-init` pulls models in the background and the first chat waits                                                  |
| GPU               | Requested by default                                 | Opt-in (uncomment the `deploy` block) so the stack starts on GPU-less hosts                                                                        |
| Observability     | Aspire dashboard                                     | Seq at http://localhost:8090 (OTLP) + `docker compose logs`                                                                                        |
| Runtime user      | Services run as the host developer (`dotnet run`)    | Images run **non-root** (`$APP_UID`) - except Sandbox (drives the host Docker socket) and Gateway (reads the mounted TLS cert), which require root |

Everything else - the seven services, the five databases, Redis, NATS JetStream, Qdrant, Azurite,
the `X-Internal-Token` security model, per-user data isolation, the Sentinel PDP, and the default
`gemma4:e4b` / `nomic-embed-text` models - matches between the two setups exactly.
