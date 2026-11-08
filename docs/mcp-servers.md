# MCP servers

Sentium agents can use tools from [Model Context Protocol](https://modelcontextprotocol.io) (MCP) servers. The
integration keeps Sentium self-hosted:

- **Local network only.** Sentium connects to HTTP servers at loopback or private addresses (10/8, 172.16/12,
  192.168/16, 100.64/10, fc00::/7). The address a host name resolves to is checked every time a connection opens, so
  a DNS change cannot redirect it to the internet.
- **No provider-side MCP.** Tools run through the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
  inside Sentium, so they work with the local llama.cpp models.
- **stdio servers run in the Sandbox.** The Sandbox starts them in hardened, network-less containers.

## Who can do what

| Role      | Can                                                                                                   |
| --------- | ----------------------------------------------------------------------------------------------------- |
| Sovereign | Register, edit and delete servers; sync tool lists; approve, enable and configure tools; MCP settings |
| Everyone  | Give approved MCP tools to their own agents (Agents → tool picker → "Selected" → `mcp · <server>`)    |

## Register a server

Open **AI & automation → MCP Servers** (Sovereign only) and choose **Register server**.

| Field     | Notes                                                                                                                                                                                                                                                                                                  |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Name      | Slug such as `home-assistant`. Tool names become `<name>__<tool>`, so the name cannot change afterwards.                                                                                                                                                                                               |
| Transport | **HTTP**: a Streamable HTTP (or SSE) endpoint on your network. **Sandbox container**: a stdio server image.                                                                                                                                                                                            |
| Secrets   | HTTP headers (e.g. `Authorization`) or container environment variables. Secret values are stored encrypted and never shown again. Unlock a value (lock icon) to store it as plain text instead - it stays visible and editable in the dialog. Use that for non-sensitive values such as `SEARXNG_URL`. |

Then:

1. **Sync** connects to the server and lists its tools. New tools start **disabled and unapproved**.
2. Review each tool's description, hints (read-only / destructive / open world) and input schema, then **Approve** it.
   Approving also enables it.
3. Decide whether each call must be approved by the user (**ask each call**, on by default). Workflows and schedules
   run unattended, so they only get tools that do not ask.
4. Add the tool to an agent: in the agent's tool picker switch to **Selected** and tick it. "All tools" never includes
   MCP tools, so registering a server never widens what existing agents can do.

### When a server changes a tool

Agents see the definition you approved, not whatever the server says now. A sync that finds a different description,
schema or set of hints marks the tool **changed - re-approve** and hides it from every agent until you approve it again.
The portal shows the description you approved next to the new one. A tool the server stops listing is marked
**missing**. Agents that still list it keep working, without that tool.

## HTTP servers

Any MCP server that speaks Streamable HTTP (or the older SSE transport) on your network works, for example a home
automation hub's MCP endpoint. The endpoint must be `http(s)://` with a loopback or private address. Public addresses
are rejected when you save, and again whenever a connection opens.

## Sandbox (stdio) servers

Many MCP servers are stdio programs. Sentium runs them in containers managed by the Sandbox, which owns the Docker
socket.

1. Enable the host:

   | Setting (`Sandbox:Mcp:*`)                                                     | Compose (`.env`)              | Default                      |
   | ----------------------------------------------------------------------------- | ----------------------------- | ---------------------------- |
   | `Enabled`                                                                     | `SANDBOX_MCP_ENABLED`         | `false` (Aspire dev: `true`) |
   | `AllowedNetworks`                                                             | `SANDBOX_MCP_ALLOWED_NETWORK` | none                         |
   | `RequireDigestPinnedImages`                                                   | -                             | `true`                       |
   | `MaxServers`, `MaxMemoryMb`, `MaxCpuLimit`, `IdleTimeoutMinutes`, `RunAsUser` | -                             | 8, 1024, 2, 30, image's user |

   There is no image allowlist: a Sovereign can register any digest-pinned image, so register only images you trust.

2. Register the server with a **digest-pinned** image, for example:

   ```bash
   docker pull mcp/time
   docker inspect --format '{{index .RepoDigests 0}}' mcp/time   # mcp/time@sha256:…
   ```

   Arguments go to the image's entrypoint, one per line (e.g. `--local-timezone=Europe/Sofia`).

Each container gets:

- no network
- a read-only root filesystem, with a small `noexec` `/tmp`
- no Linux capabilities, `no-new-privileges`
- memory, CPU and process limits

The container is stopped when idle, replaced when you edit the server, and removed if the Sandbox restarts. Sentium
starts it again on the next call.

### Images must include their dependencies

Servers launched with `npx -y …` or `uvx …` download packages when they start. With no network that fails, so build an
image that already contains the server:

```dockerfile
FROM node:22-alpine
RUN npm install -g @modelcontextprotocol/server-everything@<version>
ENTRYPOINT ["mcp-server-everything"]
```

Push it to a registry the Sandbox can pull from and reference it by digest.

### Servers that need the local network

Docker cannot limit a container to "local network only". If a stdio server must reach something on your network (for
example a self-hosted SearXNG), create an `internal: true` Docker network and allow it (`SANDBOX_MCP_ALLOWED_NETWORK`).
Then attach the target service to that network as well as its usual one. The MCP container can reach the service, but
not the internet.

`docker-compose.yml` already defines such a network, `sentium-egress` (named explicitly, because the Sandbox matches
the allowed network by exact name), and allows it by default.

## Web search (SearXNG)

The compose stack includes a [SearXNG](https://github.com/searxng/searxng) instance (`searxng`, configured in
[infra/searxng/settings.yml](../infra/searxng/settings.yml)) that agents can query through the
[mcp-searxng](https://github.com/ihor-sokoliuk/mcp-searxng) server. `searxng` is on the normal network, so it can
reach the search engines, and on `sentium-egress`. The MCP container joins only `sentium-egress`: it can reach
SearXNG, not the internet. SearXNG is not published to the host.

1. In `.env`, set `SEARXNG_SECRET` (a random value) and `SANDBOX_MCP_ENABLED=true`. The defaults already allow the
   `sentium-egress` network.
2. `docker compose up -d`.
3. In **MCP Servers**, register a server:

   | Field     | Value                                                                                                              |
   | --------- | ------------------------------------------------------------------------------------------------------------------ |
   | Name      | `searxng` (tools become `searxng__searxng_web_search`, ...)                                                        |
   | Transport | Sandbox container                                                                                                  |
   | Image     | `isokoliuk/mcp-searxng@sha256:<digest>`, e.g. from `docker buildx imagetools inspect isokoliuk/mcp-searxng:latest` |
   | Network   | `sentium-egress`                                                                                                   |
   | Secrets   | `SEARXNG_URL` = `http://searxng:8080`                                                                              |

   Optional secrets (environment variables) to keep results small for local models: `SEARXNG_MAX_RESULTS=8`,
   `SEARXNG_MAX_RESULT_CHARS=400`, `SEARXNG_LITE_TOOLS=true`.

4. **Sync**, review and **Approve** the tools, then add them to an agent as described above.

**Aspire dev stack.** The AppHost runs the same setup: a `searxng` container (secret generated as the `searxng-secret`
parameter), the `sentium-egress` network created as `internal` at start-up, and the Sandbox allowed to use that
network. There is nothing to configure: register the server as above (the MCP Servers page needs
`Mcp:SandboxEnabled`, which is on by default). If a `sentium-egress` network already exists and is not internal, the
AppHost refuses to start; remove it (`docker network rm sentium-egress`) and it is recreated. Override the network
with `Mcp:AllowedNetwork` in the AppHost configuration.

The server also exposes `web_url_read`, which fetches a page. Because its container has no internet, it can only fail,
so leave that tool unapproved. Reading pages needs a different, public-egress mechanism.

## Settings and limits

The **MCP settings** card on the MCP Servers page (Registry key `mcp`) applies to every server:

- **MCP tools enabled**: kill switch. When off, no agent gets an MCP tool and open connections are closed.
- **Connect timeout / call timeout**: connection setup and a single tool call.
- **Close idle after**: idle connections close; Sandbox containers stop with them.
- **Max result**: longer tool results are truncated before they reach the model.

## Security model

Every MCP call:

- is a Sentinel-evaluated action on resource `External` (resource id `mcp:<server>/<tool>`, action `Execute`)
- has its arguments scanned for secrets before they leave Sentium
- needs the user's approval unless you turned that off for the tool

Agents that have an MCP tool are told that external tool output is data, not instructions. All of a server's tools
share one set of credentials. See [THREAT_MODEL.md](../THREAT_MODEL.md) §3, §4.1 and §5.
