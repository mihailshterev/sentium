# Installation guide

This guide covers every supported way to install and run **Sentium** - the whole platform
(Sentium.Web, the separate Sandbox service, the portal, and all backing infrastructure):

- **[Method A - .NET Aspire](#method-a--net-aspire-recommended-for-development)** - the recommended
  path for **day-to-day development**. One command, a rich dashboard, hot reload, and no
  certificate/issuer setup.
- **[Method B - Docker Compose](#method-b--docker-compose-self-hosted-deployment)** - **self-hosted
  deployment** with plain Docker, without installing the .NET SDK or pnpm on the host.
- **[Method C - Front-end only](#method-c--front-end-only)** - run just the portal against an
  already-running backend.

Both full-stack methods run the **same** processes, wired with the same connection strings,
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

|                    | **Method A - .NET Aspire**                 | **Method B - Docker Compose**                           |
| ------------------ | ------------------------------------------ | ------------------------------------------------------- |
| **Best for**       | Local development                          | Self-hosted deployment                                  |
| **Host needs**     | .NET 10 SDK, pnpm, Docker                  | Docker only (+ .NET SDK once, for a dev cert)           |
| **Apps run**       | As host processes (hot reload)             | As built container images                               |
| **Front-end**      | Vite dev server (`:5173`, proxies the API) | Built into and served by Sentium.Web (`:8443`)          |
| **TLS / cookie**   | Handled by the Aspire HTTPS pipeline       | Sentium.Web terminates TLS with a dev cert you generate |
| **Observability**  | Aspire dashboard                           | `docker compose logs`                                   |
| **Model download** | Startup **blocks** until models are pulled | Pulled in the background; first chat waits              |
| **GPU**            | Requested by default                       | Opt-in                                                  |

Both methods require **Docker** to be running: Aspire launches the infrastructure (and the code
sandbox) as containers, and Compose builds and runs everything in containers.

---

## Method A - .NET Aspire (recommended for development)

The [.NET Aspire AppHost](../src/aspire/Sentium.AppHost/Program.cs) is the single entry point for
local development. One command starts Sentium.Web, the Sandbox, the portal, and every backing
dependency (PostgreSQL, Redis, Qdrant, Ollama, Azurite) as managed containers - with a
live dashboard, hot reload, and automatic service discovery.

### Prerequisites

| Requirement                           | Notes                                                                                                                                                                                 |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **.NET 10 SDK**                       | Pinned to `10.0.102`+ by [`global.json`](../global.json). No separate "Aspire workload" is needed - the AppHost references the Aspire SDK via NuGet, so the .NET SDK alone is enough. |
| **pnpm 11**                           | The AppHost runs `pnpm install` + Vite for the portal.                                                                                                                                |
| **Docker Desktop / Engine** (running) | Aspire starts the infrastructure containers, and the sandbox executes code via the host Docker daemon.                                                                                |
| **An NVIDIA GPU (optional)**          | The AppHost requests GPU support for Ollama by default. CPU-only works but is slow. See [GPU acceleration](#gpu-acceleration).                                                        |

### 1. Trust the local HTTPS dev certificate (one-time)

Aspire serves its dashboard and the HTTPS service endpoints with the ASP.NET Core developer
certificate:

```bash
dotnet dev-certs https --trust
```

### 2. Provide the secret parameter (first run)

The AppHost declares one **secret parameter** with no committed default: the internal API key that
Sentium.Web and the Sandbox use to authenticate to each other. Provide it either way:

- **Interactively** - on first run, the Aspire dashboard prompts for any parameter that has no
  value, and persists what you enter to user secrets.
- **Pre-seeded** - set it once with user secrets (the AppHost has a `UserSecretsId`):

  ```bash
  cd src/aspire/Sentium.AppHost
  dotnet user-secrets set "Parameters:internal-api-key" "<random-32-chars>"
  ```

The PostgreSQL password is generated by Aspire on first run and kept in the same user secrets.

### 3. Run

```bash
dotnet run --project src/aspire/Sentium.AppHost/Sentium.AppHost.csproj
```

The **Aspire dashboard** URL (with a one-time login token) is printed in the console. Open it to
watch every resource start, stream logs, and follow the model downloads.

### What gets started

Sentium.Web (all six modules), the Sandbox, the portal (`sentium-portal`), and the infrastructure
(PostgreSQL with one `sentiumdb` database, Redis, Qdrant, Ollama, Azurite). The
default models (`gemma4:e4b` + `embeddinggemma-2:740m`) are pulled into the Ollama volume; Aspire
**blocks** Sentium.Web until the pull completes.

### Endpoints

| What                    | URL                                     |
| ----------------------- | --------------------------------------- |
| **Portal** (start here) | http://localhost:5173                   |
| Aspire dashboard        | printed on startup (with a login token) |

The portal sends you to its own `/login` page to sign in or register. Sentium.Web's API reference
(Scalar) is linked from its resource in the dashboard.

> **First run takes a while** - Docker pulls the infrastructure images and Ollama downloads the
> multi-GB models. Subsequent starts reuse the persisted data volumes.

---

## Method B - Docker Compose (self-hosted deployment)

Self-host the entire platform with plain Docker Compose - Sentium.Web, the Sandbox, the portal, and
all backing infrastructure - without installing the .NET SDK or pnpm on the host. This is the
deployment-oriented companion to the [`docker-compose.yml`](../docker-compose.yml) at the repository
root.

### Prerequisites

| Requirement                                      | Notes                                                                                                       |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| **Docker Engine 24+ with the Compose v2 plugin** | `docker compose version` should print v2.x. Docker Desktop (Windows/macOS) bundles it.                      |
| **.NET SDK 10**                                  | Needed **only** for the one-time TLS dev-certificate step. The services themselves build inside containers. |
| **~20 GB free disk**                             | For base images, the three built images, the downloaded models, and the persisted data volumes.             |
| **An NVIDIA GPU (optional)**                     | Strongly recommended for usable local-LLM latency. See [GPU acceleration](#gpu-acceleration).               |

You do **not** need Node/pnpm or the .NET runtime on the host - the portal and both .NET apps are
compiled inside multi-stage image builds.

### Quick start

```bash
# 1. Configure secrets and ports
cp .env.example .env            # PowerShell: Copy-Item .env.example .env
#    …then edit the secrets in .env

# 2. Generate Sentium.Web's TLS dev certificate (use the same password as CERT_PASSWORD in .env)
mkdir certs    # dotnet dev-certs does not create the target directory
dotnet dev-certs https -ep certs/sentium.pfx -p <CERT_PASSWORD>
dotnet dev-certs https --trust

# 3. Build and start everything
docker compose up --build
```

Then open the portal at **https://localhost:8443**.

> **First run takes a while.** Docker downloads base images, builds three images, and pulls the chat +
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

See the [Configuration reference](#configuration-reference) for every variable.

#### 2. Generate the TLS certificate

Sentium.Web terminates HTTPS so the session cookie can be issued as `Secure` / `SameSite=None` to
the portal's origin. Compose mounts a developer certificate from `./certs/sentium.pfx` at
runtime - generate it with the **same password** you put in `CERT_PASSWORD`:

```bash
mkdir certs    # PowerShell: New-Item -ItemType Directory certs
dotnet dev-certs https -ep certs/sentium.pfx -p <CERT_PASSWORD>
dotnet dev-certs https --trust
```

> `dotnet dev-certs` does **not** create the output directory, so create `certs/` first.
> If you skip it and run `docker compose up`, Docker creates an empty `certs/` mount and the
> Sentium.Web fails to find the certificate.

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

On first start Sentium.Web applies every module's EF Core migrations automatically (it runs in the
`Development` environment for exactly this reason), and the one-shot `ollama-init` container pulls
the models defined by `SENTIUM_MODEL` and `SENTIUM_EMBEDDING_MODEL`, retrying until they succeed.

To stop:

```bash
docker compose down                # stop & remove containers, keep data volumes
```

### What gets started

#### Infrastructure

| Service       | Image                                     | Host port(s) | Purpose                                                                                                          |
| ------------- | ----------------------------------------- | ------------ | ---------------------------------------------------------------------------------------------------------------- |
| `postgres`    | `postgres:17-alpine`                      | 5432         | Relational data - one `sentiumdb` database, one schema per module (`identity`, `registry`, `sentinel`, `praxis`) |
| `redis`       | `redis:7-alpine`                          | 6379         | HybridCache L2                                                                                                   |
| `qdrant`      | `qdrant/qdrant:latest`                    | 6333, 6334   | Vector store for RAG (6333 REST/dashboard, 6334 gRPC)                                                            |
| `azurite`     | `mcr.microsoft.com/azure-storage/azurite` | 10000        | Azure Blob Storage emulator (workspace files & sandbox artifacts)                                                |
| `ollama`      | `ollama/ollama:0.30.5`                    | 11434        | Local LLM inference                                                                                              |
| `ollama-init` | `ollama/ollama:0.30.5`                    | -            | One-shot model puller (exits when models are ready)                                                              |

#### Application services

| Service       | Dockerfile                             | Exposed via      | Role                                                                                |
| ------------- | -------------------------------------- | ---------------- | ----------------------------------------------------------------------------------- |
| `sentium-web` | `src/hosts/Sentium.Web/Dockerfile`     | **8443** (HTTPS) | The modular monolith: every module, the API, the login session and the built portal |
| `sandbox`     | `src/hosts/Sentium.Sandbox/Dockerfile` | internal         | Stateless, hardened Docker code execution; reachable only from `sentium-web`        |

#### Endpoints once healthy

| What                    | URL                             |
| ----------------------- | ------------------------------- |
| **Portal** (start here) | https://localhost:8443          |
| Qdrant dashboard        | http://localhost:6333/dashboard |
| Ollama API              | http://localhost:11434          |

### Verifying the installation

```bash
# All containers should be "running" (and healthy where a healthcheck is defined).
docker compose ps

# Watch the model download finish - the platform's first chat depends on this.
docker compose logs -f ollama-init      # ends with "All models ready."

# Confirm Sentium.Web is reachable (expects 401, since there is no session yet).
curl -k https://localhost:8443/api/identity/account/session
```

Then open **https://localhost:8443**, register or log in, and start a conversation. The Vigil page
inside the portal shows a live health view of every module, the sandbox and each infrastructure
dependency.

### Configuration reference

All configuration lives in `.env` (see [`.env.example`](../.env.example)). Compose injects these
values plus the connection strings and service-discovery keys that mirror what Aspire provides at
runtime.

| Variable                  | Default                 | Description                                                                             |
| ------------------------- | ----------------------- | --------------------------------------------------------------------------------------- |
| `POSTGRES_PASSWORD`       | `change-me-…`           | PostgreSQL `postgres` superuser password.                                               |
| `INTERNAL_API_KEY`        | `change-me-…`           | Pre-shared `X-Internal-Token` between `sentium-web` and `sandbox`.                      |
| `QDRANT_API_KEY`          | `change-me-…`           | API key the Qdrant server requires and the clients send. Any non-empty string.          |
| `CERT_PASSWORD`           | `change-me-…`           | Password of `certs/sentium.pfx`. Must match the `dotnet dev-certs` step.                |
| `WEB_HTTPS_PORT`          | `8443`                  | Host HTTPS port for Sentium.Web (the portal and the API).                               |
| `SENTIUM_MODEL`           | `gemma4:e4b`            | Chat/reasoning model (any valid Ollama tag).                                            |
| `SENTIUM_EMBEDDING_MODEL` | `embeddinggemma-2:740m` | Embedding model. Must be 768-dimensional to match the RAG vector size.                  |
| `SANDBOX_JOBS_DIR`        | `/tmp/sentium-sandbox`  | Per-job sandbox working directory - identical on host and inside the Sandbox container. |
| `AZURITE_CONNECTION`      | _(well-known dev key)_  | Azurite blob connection string. Safe to leave as-is.                                    |

> **Changing the port?** Only `WEB_HTTPS_PORT` exists now: the portal is served by Sentium.Web from the
> same origin as the API, so there is no CORS origin or API base to keep in sync.

### Platform notes

#### Windows / WSL2

- Run Docker Desktop with the **WSL2 backend**.
- The code sandbox bind-mounts the host Docker socket and a job directory that must share an
  **identical path** on host and container (`SANDBOX_JOBS_DIR`). Point it at a path **inside your
  WSL2 distro** (the default `/tmp/sentium-sandbox` resolves correctly under the WSL2 backend).

#### macOS

Works with Docker Desktop out of the box. Apple-silicon machines run CPU inference only (no NVIDIA
GPU passthrough), so expect slower first responses.

#### Container security

Both .NET images run as **root**: the **Sandbox** drives the host Docker socket, and **Sentium.Web**
reads the mounted TLS certificate.

---

## Method C - Front-end only

If the backend is already running (via Method A or B), you can run the portal on its own with the
Vite dev server - useful for UI work with hot reload:

```bash
cd src/clients/sentium-portal
pnpm install
pnpm dev            # Vite dev server on http://localhost:5173
```

The Vite dev server proxies `/api` to Sentium.Web, so the browser stays on one origin. Point
it at a running instance with the `API_PROXY_TARGET` environment variable (default
`https://localhost:7254`, the `https` launch profile of Sentium.Web; use `https://localhost:8443`
for a Compose stack). Aspire sets it for you.

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
docker compose logs -f sentium-web
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

Persisted state lives in named volumes: `postgres-data`, `redis-data`, `qdrant-data`,
`azurite-data`, `ollama-data`. They survive `docker compose down` and are only removed by
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
Provide the `internal-api-key` parameter via the dashboard prompt or `dotnet user-secrets` (see
[Method A, step 2](#2-provide-the-secret-parameter-first-run)).

### Docker Compose (Method B)

**The build fails during `pnpm install` with `ERR_PNPM_LOCKFILE_CONFIG_MISMATCH`.**
Make sure you are on a current checkout - the frontend Dockerfiles must copy each package's
`pnpm-workspace.yaml` (which carries the pnpm `overrides`) alongside `package.json` and
`pnpm-lock.yaml` before the frozen install. This is already handled in the committed Dockerfiles.

**Browser warns the certificate is not trusted on `https://localhost:8443`.**
Run `dotnet dev-certs https --trust` (re-run after regenerating the cert), or accept the certificate
manually. Confirm `CERT_PASSWORD` in `.env` matches the password used to create `certs/sentium.pfx`.

**Agent chat says the model isn't available / hangs on the first message.**
The models are still downloading. Watch `docker compose logs -f ollama-init` until it prints
`All models ready.` First-run downloads of multi-GB models can take a while.

**`sentium-web` restarts repeatedly / can't connect to PostgreSQL.**
`sentium-web` waits for the PostgreSQL healthcheck. If `postgres` never goes healthy, check
`docker compose logs postgres` - the most common cause is an empty `POSTGRES_PASSWORD` in `.env`.

**Sandbox code execution fails.**
Verify the host Docker socket is mounted and `SANDBOX_JOBS_DIR` resolves to the same absolute path on
host and container. On Windows, that path must live inside your WSL2 distro. Every run is also
authorized by Sentinel inside `sentium-web`: if `INTERNAL_API_KEY` is empty or the two containers
disagree, runs are denied.

**Port already in use.**
Change the offending `*_PORT` value in `.env` (and rebuild so the portal picks up the new API base).

---

## Aspire vs. Compose parity

Both full-stack methods are functionally equivalent. The intentional differences are:

| Aspect            | Aspire AppHost                                       | Docker Compose                                                                                                   |
| ----------------- | ---------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| TLS / auth cookie | Handled by the Aspire HTTPS dev pipeline             | Sentium.Web terminates TLS using `certs/sentium.pfx` (one-time `dotnet dev-certs` step)                          |
| Model readiness   | Startup **blocks** until models download (`WaitFor`) | Services start immediately; `ollama-init` pulls models in the background and the first chat waits                |
| GPU               | Requested by default                                 | Opt-in (uncomment the `deploy` block) so the stack starts on GPU-less hosts                                      |
| Observability     | Aspire dashboard                                     | `docker compose logs`                                                                                            |
| Runtime user      | Apps run as the host developer (`dotnet run`)        | Both .NET images run as root (Sentium.Web reads the mounted TLS cert; the Sandbox drives the host Docker socket) |

Everything else - Sentium.Web and its six modules, the separate Sandbox, the single PostgreSQL
database, Redis, Qdrant, Azurite, the `X-Internal-Token` security model, per-user data isolation, the Sentinel PDP, and the default
`gemma4:e4b` / `embeddinggemma-2:740m` models - matches between the two setups exactly.
