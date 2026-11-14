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
- [LAN access: HTTPS on every device](#lan-access-https-on-every-device)
- [Method C - Front-end only](#method-c--front-end-only)
- [GPU acceleration](#gpu-acceleration)
- [Managing models](#managing-models)
- [Day-two operations (Compose)](#day-two-operations-compose)
- [Troubleshooting](#troubleshooting)
- [Aspire vs. Compose parity](#aspire-vs-compose-parity)

---

## Choosing a method

|                    | **Method A - .NET Aspire**                 | **Method B - Docker Compose**                        |
| ------------------ | ------------------------------------------ | ---------------------------------------------------- |
| **Best for**       | Local development                          | Self-hosted deployment                               |
| **Host needs**     | .NET 10 SDK, pnpm, Docker                  | Docker only                                          |
| **Apps run**       | As host processes (hot reload)             | As built container images                            |
| **Front-end**      | Vite dev server (`:5173`, proxies the API) | Built into and served by Sentium.Web, behind Caddy   |
| **TLS / cookie**   | Handled by the Aspire HTTPS pipeline       | Caddy, with a per-installation CA, on a LAN hostname |
| **Observability**  | Aspire dashboard                           | `docker compose logs`                                |
| **Model download** | Startup **blocks** until models are ready  | Same: `sentium-web` waits for the inference servers  |
| **GPU**            | CUDA by default (`Llm:UseGpu=false` = CPU) | Opt-in (`docker-compose.gpu.yml`)                    |

Both methods require **Docker** to be running: Aspire launches the infrastructure (and the code
sandbox) as containers, and Compose builds and runs everything in containers.

---

## Method A - .NET Aspire (recommended for development)

The [.NET Aspire AppHost](../src/aspire/Sentium.AppHost/Program.cs) is the single entry point for
local development. One command starts Sentium.Web, the Sandbox, the portal, and every backing
dependency (PostgreSQL with pgvector, Redis, the llama.cpp inference servers, Azurite) as managed containers - with a
live dashboard, hot reload, and automatic service discovery.

### Prerequisites

| Requirement                           | Notes                                                                                                                                                                                 |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **.NET 10 SDK**                       | Pinned to `10.0.102`+ by [`global.json`](../global.json). No separate "Aspire workload" is needed - the AppHost references the Aspire SDK via NuGet, so the .NET SDK alone is enough. |
| **pnpm 11**                           | The AppHost runs `pnpm install` + Vite for the portal.                                                                                                                                |
| **Docker Desktop / Engine** (running) | Aspire starts the infrastructure containers, and the sandbox executes code via the host Docker daemon.                                                                                |
| **An NVIDIA GPU (optional)**          | The AppHost runs the CUDA build of llama-server by default. Without a GPU, set `Llm:UseGpu=false`; CPU-only works but is slow. See [GPU acceleration](#gpu-acceleration).             |

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

The PostgreSQL password and the `llm-api-key` (the bearer token the inference servers require) are
generated by Aspire on first run and kept in the same user secrets.

### 3. Run

```bash
dotnet run --project src/aspire/Sentium.AppHost/Sentium.AppHost.csproj
```

The **Aspire dashboard** URL (with a one-time login token) is printed in the console. Open it to
watch every resource start, stream logs, and follow the model downloads (the `models-init` resource).

### What gets started

Sentium.Web (all six modules), the Sandbox, the portal (`sentium-portal`), and the infrastructure
(PostgreSQL with one `sentiumdb` database, Redis, Azurite, and the inference stack):

- `models-init` downloads the model files pinned in [`infra/llm/models.lock`](../infra/llm/models.lock)
  into the `sentium-llm-models` volume and verifies each SHA-256, then exits.
- `llm` runs llama.cpp's `llama-server` in router mode for the chat models declared in
  [`infra/llm/models.ini`](../infra/llm/models.ini) (default `gemma-4-e4b`), on http://localhost:8090.
- `embeddings` runs a second `llama-server` for the embedding model (`embeddinggemma-2`), on
  http://localhost:8091.

Aspire **blocks** Sentium.Web until the default chat model is loaded and the embedding server is
healthy. Both servers reject every request except `/health` without the `llm-api-key`.

### Endpoints

| What                    | URL                                     |
| ----------------------- | --------------------------------------- |
| **Portal** (start here) | http://localhost:5173                   |
| Aspire dashboard        | printed on startup (with a login token) |

The portal sends you to its own `/login` page to sign in or register. Sentium.Web's API reference
(Scalar) is linked from its resource in the dashboard.

> **First run takes a while** - Docker pulls the infrastructure images and `models-init` downloads
> about 5 GB of model files. Subsequent starts reuse the persisted data volumes; the models volume is
> shared with Docker Compose, so models download only once per machine.

---

## Method B - Docker Compose (self-hosted deployment)

Self-host the entire platform with plain Docker Compose - Sentium.Web, the Sandbox, the portal, and
all backing infrastructure - without installing the .NET SDK or pnpm on the host. This is the
deployment-oriented companion to the [`docker-compose.yml`](../docker-compose.yml) at the repository
root.

### Prerequisites

| Requirement                                      | Notes                                                                                           |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------- |
| **Docker Engine 24+ with the Compose v2 plugin** | `docker compose version` should print v2.x. Docker Desktop (Windows/macOS) bundles it.          |
| **~20 GB free disk**                             | For base images, the three built images, the downloaded models, and the persisted data volumes. |
| **An NVIDIA GPU (optional)**                     | Strongly recommended for usable local-LLM latency. See [GPU acceleration](#gpu-acceleration).   |

You do **not** need Node/pnpm or the .NET SDK on the host - the portal and both .NET apps are
compiled inside multi-stage image builds.

### Quick start

```bash
# 1. Configure secrets and the LAN hostname
cp .env.example .env            # PowerShell: Copy-Item .env.example .env
#    …then edit the secrets (and SENTIUM_HOSTNAME, default sentium.home.arpa) in .env

# 2. Build and start everything
docker compose up --build

# 3. Export this installation's root certificate, to trust on each device
docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt ./sentium-root-ca.crt
```

Point the hostname at the host and trust the certificate on each device
([LAN access](#lan-access-https-on-every-device)), then open the portal at **https://sentium.home.arpa**.

> **First run takes a while.** Docker downloads base images, builds three images, and `models-init`
> downloads about 5 GB of model files. `sentium-web` starts once the default chat model is loaded and
> the embedding server is healthy (watch `docker compose logs -f models-init llm`).

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

#### 2. Choose the hostname

`SENTIUM_HOSTNAME` (default `sentium.home.arpa`) is the name every device uses to reach Sentium; the
`caddy` service issues the portal's certificate for it. `home.arpa` is reserved for home networks
(RFC 8375), so the name never collides with a real domain. To also use the portal on the host itself
as `https://localhost`, list both: `SENTIUM_HOSTNAME=sentium.home.arpa, localhost`.

#### 3. Build and start

```bash
docker compose up --build          # foreground, streams all logs
# or
docker compose up --build -d       # detached / background
```

On first start Sentium.Web applies every module's EF Core migrations automatically (it runs in the
`Development` environment for exactly this reason), and the one-shot `models-init` container downloads
the model files pinned in `infra/llm/models.lock`, verifying each SHA-256 and resuming interrupted
downloads. A checksum mismatch makes it exit non-zero, which keeps the inference servers down.

To stop:

```bash
docker compose down                # stop & remove containers, keep data volumes
```

#### 4. Reach it from your devices

Follow [LAN access](#lan-access-https-on-every-device): make the hostname resolve to the host, then
trust the installation's root certificate on each device.

### What gets started

#### Infrastructure

| Service       | Image                                                      | Host port(s)   | Purpose                                                                                                                                                                                                                         |
| ------------- | ---------------------------------------------------------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `postgres`    | `pgvector/pgvector:pg18-trixie`                            | 5432           | Relational data + RAG vectors (pgvector) - one `sentiumdb` database, one schema per module (`identity`, `registry`, `sentinel`, `codex`, `locus`, `tempus`, `praxis`, `mcp`; the Quartz scheduler keeps its tables in `praxis`) |
| `redis`       | `redis:7-alpine`                                           | 6379           | HybridCache L2                                                                                                                                                                                                                  |
| `azurite`     | `mcr.microsoft.com/azure-storage/azurite`                  | 10000          | Azure Blob Storage emulator (workspace files, sandbox artifacts & inventory attachments)                                                                                                                                        |
| `models-init` | `curlimages/curl:8.22.0` (pinned digest)                   | -              | One-shot: downloads and checksum-verifies the model files from `infra/llm/models.lock`                                                                                                                                          |
| `llm`         | `ghcr.io/ggml-org/llama.cpp:server-b11804` (pinned digest) | 127.0.0.1:8090 | Chat models: `llama-server` in router mode, presets from `infra/llm/models.ini`                                                                                                                                                 |
| `embeddings`  | `ghcr.io/ggml-org/llama.cpp:server-b11804` (pinned digest) | 127.0.0.1:8091 | The RAG embedding model (768 dimensions)                                                                                                                                                                                        |

#### Application services

| Service       | Image / Dockerfile                     | Exposed via                         | Role                                                                                                      |
| ------------- | -------------------------------------- | ----------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `caddy`       | `caddy:2.10.2-alpine`                  | **443** (HTTPS), 80 (redirect only) | TLS for `SENTIUM_HOSTNAME` from a per-installation CA (`infra/caddy/Caddyfile`); proxies to `sentium-web` |
| `sentium-web` | `src/hosts/Sentium.Web/Dockerfile`     | internal (HTTP 8080)                | The modular monolith: every module, the API, the login session and the built portal                       |
| `sandbox`     | `src/hosts/Sentium.Sandbox/Dockerfile` | internal                            | Stateless, hardened Docker code execution; reachable only from `sentium-web`                              |

#### Endpoints once healthy

| What                          | URL                                          |
| ----------------------------- | -------------------------------------------- |
| **Portal** (start here)       | https://sentium.home.arpa                    |
| Chat inference (llama-server) | http://127.0.0.1:8090 (bearer `LLM_API_KEY`) |
| Embeddings (llama-server)     | http://127.0.0.1:8091 (bearer `LLM_API_KEY`) |

### Verifying the installation

```bash
# All containers should be "running" (and healthy where a healthcheck is defined).
docker compose ps

# Watch the model download finish - sentium-web waits for it.
docker compose logs -f models-init      # ends with "All models ready."

# Both inference servers answer /health without a key (200 once their model is loaded).
curl http://127.0.0.1:8091/health

# Confirm the portal is reachable over trusted HTTPS (expects 401, since there is no session yet).
curl --cacert sentium-root-ca.crt https://sentium.home.arpa/api/identity/account/session
```

Then open **https://sentium.home.arpa**, register or log in, and start a conversation. The Vigil page
inside the portal shows a live health view of every module, the sandbox and each infrastructure
dependency.

### Configuration reference

All configuration lives in `.env` (see [`.env.example`](../.env.example)). Compose injects these
values plus the connection strings and service-discovery keys that mirror what Aspire provides at
runtime.

| Variable                  | Default                | Description                                                                             |
| ------------------------- | ---------------------- | --------------------------------------------------------------------------------------- |
| `POSTGRES_PASSWORD`       | `change-me-…`          | PostgreSQL `postgres` superuser password.                                               |
| `INTERNAL_API_KEY`        | `change-me-…`          | Pre-shared `X-Internal-Token` between `sentium-web` and `sandbox`.                      |
| `SENTIUM_HOSTNAME`        | `sentium.home.arpa`    | LAN hostname the certificate is issued for; comma-separate several names.               |
| `HTTPS_PORT`              | `443`                  | Host HTTPS port of `caddy` (the portal and the API).                                    |
| `HTTP_PORT`               | `80`                   | Host HTTP port of `caddy`; it only redirects to HTTPS.                                  |
| `LLM_API_KEY`             | `change-me-…`          | Bearer token the `llm` and `embeddings` servers require from `sentium-web`.             |
| `SENTIUM_MODEL`           | `gemma-4-e4b`          | Default chat model: a section name in `infra/llm/models.ini`.                           |
| `SENTIUM_EMBEDDING_MODEL` | `embeddinggemma-2`     | Names the embedding model; change it with the model so stored knowledge is re-embedded. |
| `HF_TOKEN`                | _(empty)_              | Hugging Face token, only for models from gated repositories.                            |
| `SANDBOX_JOBS_DIR`        | `/tmp/sentium-sandbox` | Per-job sandbox working directory - identical on host and inside the Sandbox container. |
| `AZURITE_CONNECTION`      | _(well-known dev key)_ | Azurite blob connection string. Safe to leave as-is.                                    |

> **Changing the port?** With `HTTPS_PORT` other than 443, include it in the URL
> (`https://sentium.home.arpa:8443`); the port-80 redirect then points at the wrong port, so use the
> HTTPS URL directly. The portal is served from the same origin as the API, so there is no CORS origin
> or API base to keep in sync.

### Platform notes

#### Windows / WSL2

- Run Docker Desktop with the **WSL2 backend**.
- The code sandbox bind-mounts the host Docker socket and a job directory that must share an
  **identical path** on host and container (`SANDBOX_JOBS_DIR`). Point it at a path **inside your
  WSL2 distro** (the default `/tmp/sentium-sandbox` resolves correctly under the WSL2 backend).

#### macOS

Works with Docker Desktop out of the box with the CPU image. Linux containers get no GPU on macOS, so
expect slower responses; for Aspire, set `Llm:UseGpu=false`.

#### Container security

Both .NET images run as **root**. The **Sandbox** needs it to drive the host Docker socket; Sentium.Web
no longer reads a mounted certificate, but its image does not switch to the non-root `app` user yet.

---

## LAN access: HTTPS on every device

The Compose stack serves the portal at `https://<SENTIUM_HOSTNAME>` to every device on your network.
Browsers allow installing the portal as an app and using the microphone only over HTTPS when the host
is not `localhost`, so this is the supported way to use Sentium from phones and other PCs.

The `caddy` service creates a **certificate authority for this installation** on first start, issues the
portal's certificate from it and renews it automatically. The CA's private keys stay in the
`caddy-data` volume and never leave the host; devices only receive the public root certificate. Each
device needs two things: the hostname must resolve to the host, and the root certificate must be
trusted.

> Sentium is not meant to face the internet. To use it away from home, connect to your home network
> through a VPN of your choice (for example WireGuard or Tailscale); the same hostname and certificate
> then work unchanged.

### 1. Make the hostname resolve

Give the host a fixed LAN address first (a DHCP reservation on your router), then either:

- **Router DNS (recommended, works for every device including phones).** Add a local DNS record mapping
  `sentium.home.arpa` to the host's address. Most routers call this "local DNS", "static DNS" or
  "hostnames"; Pi-hole, AdGuard Home and OpenWrt have the same option.
- **Hosts file (per device).** Add a line such as `192.168.1.20  sentium.home.arpa`:
  - Windows: `C:\Windows\System32\drivers\etc\hosts` (edit as Administrator)
  - Linux / macOS: `/etc/hosts`
  - Android cannot edit its hosts file without root, so phones need the router DNS entry.

On the host itself, `127.0.0.1  sentium.home.arpa` in the hosts file works too.

A device that bypasses your router's DNS cannot resolve the name: on Android, set
**Settings → Network & internet → Private DNS** to _Automatic_ or _Off_, not a named provider.

Make sure the host's firewall allows inbound TCP 443 (and 80 for the redirect).

### 2. Export the root certificate

After the stack has started once:

```bash
docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt ./sentium-root-ca.crt

# Note the fingerprint; compare it on each device before trusting the file.
openssl x509 -in sentium-root-ca.crt -noout -fingerprint -sha256
```

The file is public and safe to copy to devices (USB, a file share, a message to yourself). Don't serve
it over plain HTTP on the network: whoever can tamper with that download could hand out their own CA
instead.

### 3. Trust the root certificate

**Windows** (Edge, Chrome and Firefox use the Windows store):

```powershell
# In an elevated PowerShell
Import-Certificate -FilePath .\sentium-root-ca.crt -CertStoreLocation Cert:\LocalMachine\Root
```

Or double-click the file → **Install Certificate** → **Local Machine** → **Place all certificates in the
following store** → **Trusted Root Certification Authorities**. Restart the browser.

**Android** (Android 11+; menu names vary by vendor):

1. Copy `sentium-root-ca.crt` to the phone.
2. **Settings → Security & privacy → More security settings → Encryption & credentials → Install a
   certificate → CA certificate**, accept the warning, and pick the file.
3. Chrome and other Chromium browsers trust it immediately. Firefox for Android needs **Settings →
   About Firefox**, tap the logo five times, then **Settings → Secret settings → Use third party CA
   certificates**.

**Linux** (system store, used by curl and most tools):

```bash
# Debian / Ubuntu
sudo cp sentium-root-ca.crt /usr/local/share/ca-certificates/sentium-root-ca.crt
sudo update-ca-certificates

# Fedora / RHEL
sudo cp sentium-root-ca.crt /etc/pki/ca-trust/source/anchors/
sudo update-ca-trust
```

Chrome and Firefox on Linux keep their own certificate lists:

- **Chrome:** **Settings → Privacy and security → Security → Manage certificates → Custom → Installed by
  you → Import**, or from a shell (needs `libnss3-tools`):
  `certutil -d sql:$HOME/.pki/nssdb -A -t "C,," -n "Sentium Local CA" -i sentium-root-ca.crt`
- **Firefox:** **Settings → Privacy & Security → Certificates → View Certificates → Authorities →
  Import**, and tick _Trust this CA to identify websites_.

**macOS / iOS:** open the file; on macOS set it to _Always Trust_ in Keychain Access, on iOS install the
profile and then enable it under **Settings → General → About → Certificate Trust Settings**.

### 4. Check it

Open `https://sentium.home.arpa` on the device: it loads with no certificate warning. The portal's own
certificate is short-lived and renewed by Caddy in the background; only the root (valid for ten years)
is installed on devices.

### Changing the hostname or replacing the CA

- **New hostname:** change `SENTIUM_HOSTNAME` and run `docker compose up -d caddy`. The CA stays the
  same, so devices keep trusting it; only DNS needs updating.
- **New CA** (for example if the host was compromised): `docker compose down`, delete the `caddy-data`
  volume (`docker volume rm sentium_caddy-data`), start again, then re-export the root and replace it on
  every device (remove the old "Sentium Local CA" entry first).

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
`https://localhost:7254`, the `https` launch profile of Sentium.Web; use `https://sentium.home.arpa`
for a Compose stack). Aspire sets it for you.

---

## GPU acceleration

Local inference is slow on CPU. To use an NVIDIA GPU you need the
[NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
installed on the host.

- **Aspire (Method A):** the CUDA build of llama-server (`server-cuda-b11804`, CUDA 12) runs **by
  default** with `--gpus all`. On a host without an NVIDIA GPU, switch to the CPU build:

  ```bash
  cd src/aspire/Sentium.AppHost
  dotnet user-secrets set "Llm:UseGpu" "false"
  ```

- **Compose (Method B):** GPU is **opt-in** so the stack still starts on GPU-less hosts. Add the
  [`docker-compose.gpu.yml`](../docker-compose.gpu.yml) override, which swaps in the CUDA image and
  reserves the GPU:

  ```bash
  docker compose -f docker-compose.yml -f docker-compose.gpu.yml up -d
  ```

Layers that do not fit in free VRAM stay on the CPU automatically (llama-server's `--fit` is on by
default), so smaller GPUs still help. Both methods run the same models with the same settings: flash
attention, a q8_0 KV cache, and 2 parallel slots of 16k tokens for the chat model (see
`infra/llm/models.ini`).

---

## Managing models

Which models exist is deployment configuration, not a runtime setting: the portal's **Models** page
shows each declared chat model with its status and lets a Sovereign **load** it ahead of its first
request or **unload** it to free GPU memory, but it cannot download or delete models.

- **[`infra/llm/models.lock`](../infra/llm/models.lock)** pins every model file: SHA-256, file name,
  and a Hugging Face download URL fixed to a commit revision. `models-init` downloads and verifies it.
- **[`infra/llm/models.ini`](../infra/llm/models.ini)** declares the chat models for the `llm` router.
  The section name is the model id agents, settings and the portal use; keys are `llama-server`
  arguments (`ctx-size`, `parallel`, ...).

**Add a chat model:** add its file to `models.lock` (take the SHA-256 from
`https://huggingface.co/api/models/<repo>/tree/<revision>`), add a `[model-id]` section with
`model = /models/<file>` to `models.ini`, and restart the stack (`models-init` downloads the file, the
router picks up the preset). Without `load-on-startup = true` the model loads on its first request.

**Change the embedding model:** update `models.lock`, the embeddings server's `--model` argument (in the
AppHost and `docker-compose.yml`), and `SENTIUM_EMBEDDING_MODEL` / `Rag:EmbeddingModelName`. It must
produce 768-dimensional vectors. On the next start Sentium.Web re-embeds every stored knowledge chunk
with the new model in the background; search quality is reduced until it finishes.

**Upgrade llama.cpp:** change the image tag and digest in `docker-compose.yml`,
`docker-compose.gpu.yml` and the AppHost's `LlamaConfig`, together. Re-check tool calling, thinking
output and embeddings against the new build before rolling it out.

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

# Full reset - delete ALL persisted data (databases, vectors, blobs, downloaded models, the CA)
docker compose down -v

# Fetch newly added models without a full reset (see Managing models)
docker compose up -d models-init && docker compose restart llm embeddings
```

Persisted state lives in named volumes: `postgres-data`, `redis-data`, `azurite-data`,
`web-dataprotection`, `caddy-data` (the installation's certificate authority) and `sentium-llm-models`
(the verified model files, shared with the Aspire AppHost). They survive `docker compose down` and are
only removed by `down -v`, which also deletes the downloaded models and creates a **new CA** on the next
start - every device then has to trust the new root certificate.

---

## Troubleshooting

### Aspire (Method A)

**The dashboard or a service fails with a TLS / certificate error.**
Run `dotnet dev-certs https --trust` (then restart). On Linux you may need to trust the cert in your
browser manually.

**Startup hangs waiting on a resource / a service never goes green.**
Open the dashboard and check the resource's logs. The most common cause on first run is the model
download (`models-init`, about 5 GB) - the inference servers and Sentium.Web wait until it finishes.
If `models-init` exits with an error, a file failed its checksum or could not be downloaded.

**`llm` or `embeddings` fails to start with a GPU error.**
The host has no usable NVIDIA GPU or container toolkit. Set `Llm:UseGpu=false` (see
[GPU acceleration](#gpu-acceleration)).

**A service errors that a parameter has no value.**
Provide the `internal-api-key` parameter via the dashboard prompt or `dotnet user-secrets` (see
[Method A, step 2](#2-provide-the-secret-parameter-first-run)).

### Docker Compose (Method B)

**The build fails during `pnpm install` with `ERR_PNPM_LOCKFILE_CONFIG_MISMATCH`.**
Make sure you are on a current checkout - the frontend Dockerfiles must copy each package's
`pnpm-workspace.yaml` (which carries the pnpm `overrides`) alongside `package.json` and
`pnpm-lock.yaml` before the frozen install. This is already handled in the committed Dockerfiles.

**Browser warns the certificate is not trusted.**
The device does not trust this installation's root certificate yet, or trusts an older one (after
`down -v` or a deleted `caddy-data` volume the CA is new). Re-export it and follow
[Trust the root certificate](#3-trust-the-root-certificate). On Linux, Chrome and Firefox keep their own
lists.

**The site can't be reached, or the certificate is for a different name.**
The hostname does not resolve to the host, or you are browsing to a name that is not in
`SENTIUM_HOSTNAME` (an IP address included). Check with `nslookup sentium.home.arpa` from the device and
see [Make the hostname resolve](#1-make-the-hostname-resolve).

**`caddy` fails to start with "address already in use".**
Something on the host already listens on port 80 or 443. Change `HTTP_PORT` / `HTTPS_PORT` in `.env`.

**`sentium-web` does not start / waits on `llm` or `embeddings`.**
The models are still downloading or loading. Watch `docker compose logs -f models-init llm` until
`models-init` prints `All models ready.` and the router reports the model loaded. If `models-init`
fails with `FAILED`, the download was interrupted repeatedly or a checksum did not match; re-run it.

**Agent chat fails with "model ... not found".**
The model id is not declared in `infra/llm/models.ini`. Check `SENTIUM_MODEL` and the agent's model.

**`sentium-web` restarts repeatedly / can't connect to PostgreSQL.**
`sentium-web` waits for the PostgreSQL healthcheck. If `postgres` never goes healthy, check
`docker compose logs postgres` - the most common cause is an empty `POSTGRES_PASSWORD` in `.env`.

**Sandbox code execution fails.**
Verify the host Docker socket is mounted and `SANDBOX_JOBS_DIR` resolves to the same absolute path on
host and container. On Windows, that path must live inside your WSL2 distro. Every run is also
authorized by Sentinel inside `sentium-web`: if `INTERNAL_API_KEY` is empty or the two containers
disagree, runs are denied.

**Port already in use.**
Change the offending `*_PORT` value in `.env`.

---

## Aspire vs. Compose parity

Both full-stack methods are functionally equivalent. The intentional differences are:

| Aspect            | Aspire AppHost                                         | Docker Compose                                                                                              |
| ----------------- | ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| TLS / auth cookie | Handled by the Aspire HTTPS dev pipeline (`localhost`) | Caddy terminates TLS for the LAN hostname with a per-installation CA; Sentium.Web runs plain HTTP behind it |
| Model readiness   | Startup **blocks** until models are ready (`WaitFor`)  | Same, via `depends_on: service_healthy` on `llm` and `embeddings`                                           |
| GPU               | CUDA by default (`Llm:UseGpu=false` for CPU)           | Opt-in (`docker-compose.gpu.yml`) so the stack starts on GPU-less hosts                                     |
| Inference API key | Generated by Aspire (`llm-api-key` user secret)        | `LLM_API_KEY` in `.env`                                                                                     |
| Observability     | Aspire dashboard                                       | `docker compose logs`                                                                                       |
| Runtime user      | Apps run as the host developer (`dotnet run`)          | Both .NET images run as root (the Sandbox drives the host Docker socket)                                    |

Everything else - Sentium.Web and its six modules, the separate Sandbox, the single PostgreSQL
database (with pgvector), Redis, Azurite, the `X-Internal-Token` security model, per-user data isolation, the Sentinel PDP, the
pinned llama.cpp build, and the default `gemma-4-e4b` / `embeddinggemma-2` models - matches between the two setups exactly.
