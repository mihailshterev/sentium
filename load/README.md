# Sentium load tests

Performance tests for Sentium.Web, written for [k6](https://grafana.com/docs/k6/) and run from its pinned Docker image. The suite adds no npm or NuGet dependencies. Every run is judged against explicit service-level objectives (SLOs) and against the last accepted baseline.

```bash
load/run.sh smoke                 # 1 min sanity check (~5 VUs)
load/run.sh load                  # expected peak, 27 min
VUS=40 HOLD=10m load/run.sh load  # reshape a profile
load/run.sh stress | spike | soak | breakpoint
```

You need Docker and bash (Git Bash on Windows). Node is optional and only used for the baseline comparison. In CI, run **Actions → Sentium.Load → Run workflow** and pick a profile. The results are attached to the run as an artifact, and the baseline comparison appears in the job summary.

## What is under test

`compose.load.yaml` starts a self-contained stack. `run.sh` wipes it before every run, so runs are independent:

| Service       | What                                                                                                                                        |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `sentium-web` | built from the production `src/hosts/Sentium.Web/Dockerfile`, `Testing` environment, **2 CPUs / 2 GB** (`LOAD_WEB_CPUS`, `LOAD_WEB_MEMORY`) |
| `postgres`    | pgvector, **default durability** (fsync on, unlike e2e), 2 CPUs / 2 GB (`LOAD_DB_CPUS`, `LOAD_DB_MEMORY`)                                   |
| `storage`     | SeaweedFS, as in e2e                                                                                                                        |
| `llm-stub`    | `stub/llm-stub.mjs`: an OpenAI-compatible stand-in for llama.cpp with a latency model                                                       |
| `k6`          | the load generator (`tools` profile, started by `run.sh`), inside the same network                                                          |

The resource limits are what make numbers comparable across machines and runs. Report them alongside any number you quote.

**Inference is simulated on purpose.** Real model speed depends on hardware and would drown out everything Sentium does. The stub streams `STUB_TOKENS` (60) tokens at `STUB_TOKENS_PER_SEC` (40/s) after `STUB_TTFT_MS` (300 ms), embeds in `STUB_EMBED_MS` (25 ms), and returns deterministic 768-dimensional vectors. The suite therefore measures queueing, persistence, history loading, streaming, pgvector search and the HTTP stack. To size inference capacity, point `BASE_URL` at a full compose install instead (see below).

## Workload model

Each virtual user (VU) is a household member with its own account (`loaduser-0001…`, registered by `setup()`). It signs in once, keeps its cookie, and pauses between actions with think time. Five scenarios run in parallel, and every profile splits its load across them in the same proportions (`WEIGHTS` in `k6/profiles.js`):

| Scenario | Share | Each iteration                                                                                                                                       |
| -------- | ----: | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `browse` |   60% | opens a portal page (home, assistant, inventory, timeline, notifications, settings, skills) with the parallel GETs that page fires; 1–4 s think time |
| `write`  |   15% | creates, edits and deletes an inventory asset, a timeline task or a conversation; 2–5 s think time                                                   |
| `chat`   |   15% | queues an assistant turn and follows its SSE stream to the terminal `done` frame; 4 turns per conversation; 4–10 s think time                        |
| `search` |    7% | semantic search over a 60-document shared corpus (embedding + pgvector) or conversation text search; 2–6 s think time                                |
| `auth`   |    3% | sign in, check session, sign out; 5–10 s think time                                                                                                  |

`setup()` signs in as the Sovereign the Testing environment seeds (`testuser`), opens registration, registers one account per VU, creates a shared inventory location and ingests the corpus. `teardown()` removes the corpus and the location and restores the registration setting. Accounts stay, because seeding is idempotent.

## Test types

| Profile      | Shape (defaults)                                                    | Answers                                                                               |
| ------------ | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `smoke`      | 1 VU per scenario × 1 min                                           | Do the scripts and the stack work? Zero errors allowed.                               |
| `load`       | ramp to 20 VUs in 5 min, hold 20 min, ramp down                     | Are the SLOs met at the expected peak, with headroom?                                 |
| `stress`     | 130 → 333 → 670 → 1000 VUs, 5 min per step                          | Where do the SLOs start to break, and how does it degrade?                            |
| `spike`      | 5 → 600 VUs in 30 s, hold 2 min, back to 5 for 5 min                | Does a burst cause errors, and does the system recover by itself?                     |
| `soak`       | 15 VUs for 2 h                                                      | Are there leaks (memory, connections, SSE relay, caches)?                             |
| `breakpoint` | arrival rate ramping to 200 iterations/s over 30 min, no think time | What is the maximum sustainable throughput? Aborts at the first sustained SLO breach. |

`VUS` scales the peak (for breakpoint, the peak rate) and `HOLD` sets the plateau. `NO_THINK=1` removes think time. `SCENARIOS=browse,write,search,auth` runs a subset, which keeps its relative weights and takes the whole load.

**Chat saturates first, by design.** Praxis runs at most `Assistant:MaxConcurrentTurns` (8) turns at once, and the rest queue. With the stub's 1.8 s answers, that caps turns at about 8 ÷ 1.9 s ≈ 4 per second; above that, `chat_turn_ms` grows with the queue. A full-mix breakpoint therefore stops on chat at about 5 turns/s, long before the HTTP paths are busy. To measure those, leave chat out: `SCENARIOS=browse,write,search,auth VUS=400 load/run.sh breakpoint`. In production, llama.cpp's `parallel` slots (2 in `infra/llm/models.ini`) cap chat well below Sentium's 8.

## Capacity (measured 2026-11-28, 2 CPU / 2 GB sentium-web)

| Path                               | Limit                                                        | What runs out                                                    |
| ---------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------- |
| Assistant turns                    | about 4 turns/s (8 concurrent × 1.9 s stub answers)          | `Assistant:MaxConcurrentTurns`; turns queue beyond it            |
| HTTP (browse, write, search, auth) | about 220 iterations/s, roughly 450 requests/s               | sentium-web CPU (≈1.6 of 2 cores); Postgres stays near 0.2 cores |
| Real household load                | 150 members with think time ≈ 64 requests/s, under 0.3 cores | nothing                                                          |

Past the HTTP limit the failure is a cascade, not a clean refusal: requests slow down, hold database connections longer, the Npgsql pool (100) runs dry, and requests fail with 500 after waiting 15 s for a connection. Sentium sheds no load before that point.

## Service-level objectives

Defined in `k6/slo.js`. A breach makes k6 exit with code 99, which fails the CI job.

| Objective                                      | Threshold                                               |
| ---------------------------------------------- | ------------------------------------------------------- |
| Failed requests (`http_req_failed`)            | < 1% (smoke: 0)                                         |
| Checks passing                                 | > 99%                                                   |
| Reads (`type:read`)                            | p95 < 300 ms, p99 < 800 ms                              |
| Writes (`type:write`)                          | p95 < 500 ms, p99 < 1 s                                 |
| Search (`type:search`)                         | p95 < 800 ms, p99 < 1.5 s                               |
| Sign-in (`type:auth`)                          | p95 < 800 ms (password hashing dominates)               |
| Queueing a turn (`chat_accept_ms`)             | p95 < 300 ms                                            |
| Complete answer (`chat_turn_ms`)               | p95 < simulated generation + `CHAT_OVERHEAD_MS` (1.5 s) |
| Turns ending in `done` (`chat_turn_completed`) | > 99%                                                   |

The chat objective follows the stub's settings. If you slow the stub down, the budget grows with it, so only Sentium's own overhead is held to a fixed bound.

## Results

`load/results/` is gitignored and holds:

- `<profile>-summary.json`: the full k6 summary.
- `<profile>-report.html`: k6's web-dashboard report, with time series for latency, throughput and VUs.
- `<profile>-docker-stats.csv`: CPU, memory, network, block I/O and PIDs of `sentium-web`, `postgres` and the stub every 5 s. In a soak run, memory that keeps climbing while load is flat is the leak signal.
- `<profile>-comparison.md`: the baseline comparison.

## Baselines

`scripts/compare.mjs` compares the p95 and p99 of every latency metric that carries a threshold against `baselines/<profile>.json`. A metric fails if it got more than `TOLERANCE` percent slower (default 15) and more than 5 ms slower. Until a baseline is committed, the comparison passes with a notice.

To accept a run as the new baseline, ideally from the CI runner so the hardware is consistent:

```bash
node load/scripts/compare.mjs load/results/load-summary.json load/baselines/load.json --update
```

Refresh baselines deliberately, after an intended performance change, and say so in the PR.

## Testing another stack

`BASE_URL=http://host:port load/run.sh load` skips the stack and targets that URL. It must be reachable from the k6 container (on Docker Desktop, use `http://host.docker.internal:port`). The target must have the `testuser` Sovereign (`SOVEREIGN_USER`, `SOVEREIGN_PASSWORD`) and the model in `MODEL`. Setup opens registration for the duration of the run.

## Limitations

- k6 core has no streaming SSE client. A turn is measured from queueing to the terminal frame (the complete answer), not to the first token. The always-open `/api/realtime/stream` is not exercised.
- Agents never call tools in this workload, because the stub never returns tool calls. Sentinel's per-tool-call policies, approvals and the sandbox are outside the measured path.
- Workflows, orchestration, scheduled jobs, file uploads and the Satelles device APIs are not part of the mix yet. Add them as further scenarios with their own weight and SLOs.
