#!/usr/bin/env bash
# Runs one load-test profile end to end: (re)creates the stack in compose.load.yaml, waits for Sentium.Web, runs k6,
# samples container resources, then compares the result with the committed baseline.
#
#   load/run.sh <smoke|load|stress|spike|soak|breakpoint>
#
# Environment (all optional):
#   VUS, HOLD, NO_THINK            reshape the profile (see k6/lib/config.js)
#   STUB_TTFT_MS, STUB_TOKENS, STUB_TOKENS_PER_SEC, STUB_EMBED_MS   the LLM stub's latency model
#   LOAD_WEB_CPUS, LOAD_WEB_MEMORY, LOAD_DB_CPUS, LOAD_DB_MEMORY      resource limits of the system under test
#   LOAD_KEEP_STACK=1              reuse a running stack instead of wiping it (faster reruns, data accumulates)
#   LOAD_SKIP_BUILD=1              don't rebuild the sentium-web image
#   BASE_URL                       test another stack (reachable from the k6 container); nothing is started then
#   TOLERANCE                      allowed p95/p99 regression against the baseline, in percent (default 15)
set -euo pipefail

profile="${1:-smoke}"
here="$(cd "$(dirname "$0")" && pwd)"
results="$here/results"
web_port="${LOAD_WEB_PORT:-25280}"

# Git Bash would otherwise rewrite container paths such as /scripts/main.js into Windows paths.
export MSYS_NO_PATHCONV=1
export PROFILE="$profile"

# ...which also stops it translating /d/... for docker, so hand docker a native path (pwd -W exists only in Git Bash).
native_here="$(cd "$here" && (pwd -W 2>/dev/null || pwd))"
compose=(docker compose -f "$native_here/compose.load.yaml")

mkdir -p "$results"
# The k6 image runs as an unprivileged user that must be able to write the reports into the bind mount.
chmod 777 "$results" 2>/dev/null || true
rm -f "$results/$profile-summary.json" "$results/$profile-report.html" "$results/$profile-docker-stats.csv"

sampler_pid=""
stop_sampler() {
  if [ -n "$sampler_pid" ]; then
    kill "$sampler_pid" 2>/dev/null || true
    wait "$sampler_pid" 2>/dev/null || true
  fi
}
trap stop_sampler EXIT

if [ -z "${BASE_URL:-}" ]; then
  if [ "${LOAD_KEEP_STACK:-0}" != "1" ]; then
    echo "==> Wiping the load stack"
    "${compose[@]}" down --volumes --remove-orphans
  fi

  build_flag=(--build)
  [ "${LOAD_SKIP_BUILD:-0}" = "1" ] && build_flag=()

  echo "==> Starting the load stack"
  "${compose[@]}" up -d --wait "${build_flag[@]}" postgres storage llm-stub sentium-web

  echo "==> Waiting for Sentium.Web on :$web_port"
  for _ in $(seq 1 150); do
    if curl -fsS "http://localhost:$web_port/alive" >/dev/null 2>&1; then
      ready=1
      break
    fi
    sleep 2
  done
  if [ "${ready:-0}" != "1" ]; then
    echo "Sentium.Web did not become ready; its logs:" >&2
    "${compose[@]}" logs --no-color --tail 200 sentium-web >&2
    exit 1
  fi

  # CPU and memory of the system under test every 5 s: the trend that shows leaks in a soak run.
  containers=$("${compose[@]}" ps -q sentium-web postgres llm-stub | tr '\n' ' ')
  stats_file="$results/$profile-docker-stats.csv"
  echo "timestamp,container,cpu_percent,memory_usage,net_io,block_io,pids" >"$stats_file"
  (
    while true; do
      # shellcheck disable=SC2086
      docker stats --no-stream --format '{{.Name}},{{.CPUPerc}},{{.MemUsage}},{{.NetIO}},{{.BlockIO}},{{.PIDs}}' $containers 2>/dev/null |
        sed "s/^/$(date -u +%Y-%m-%dT%H:%M:%SZ),/" >>"$stats_file" || true
      sleep 5
    done
  ) &
  sampler_pid=$!
fi

# Forward the tuning knobs to k6 (-e) so the scripts and the SLOs see the same values as the stub.
k6_env=(-e "PROFILE=$profile")
for name in VUS HOLD NO_THINK SCENARIOS STUB_TTFT_MS STUB_TOKENS STUB_TOKENS_PER_SEC CHAT_OVERHEAD_MS KB_DOCUMENTS \
  TURNS_PER_CONVERSATION LOAD_USER_PASSWORD SOVEREIGN_USER SOVEREIGN_PASSWORD MODEL; do
  if [ -n "${!name:-}" ]; then
    k6_env+=(-e "$name=${!name}")
  fi
done

echo "==> Running k6 ($profile)"
set +e
"${compose[@]}" run --rm --no-deps k6 run "${k6_env[@]}" /scripts/main.js
status=$?
set -e
stop_sampler
sampler_pid=""

summary="$results/$profile-summary.json"
baseline="$here/baselines/$profile.json"
if [ -f "$summary" ] && command -v node >/dev/null 2>&1; then
  echo "==> Comparing with the baseline"
  set +e
  node "$native_here/scripts/compare.mjs" "$native_here/results/$profile-summary.json" "$native_here/baselines/$profile.json" \
    --tolerance "${TOLERANCE:-15}" --markdown "$native_here/results/$profile-comparison.md"
  compare_status=$?
  set -e
  if [ "$status" -eq 0 ] && [ "$compare_status" -ne 0 ]; then
    status=$compare_status
  fi
fi

echo "==> Results in $results (k6 exit code $status; 99 = an SLO threshold failed)"
exit "$status"
