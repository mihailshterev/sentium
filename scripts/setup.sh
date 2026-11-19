#!/usr/bin/env bash
# First-run bootstrap for a development checkout: checks the prerequisites, creates .env with fresh random
# secrets, and installs the JavaScript dependencies of the portal, Satelles and e2e projects.
#
#   scripts/setup.sh [--skip-install]
#
# Safe to re-run: an existing .env is never overwritten, only placeholder secrets in it are filled in.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

ENV_FILE=".env"
ENV_TEMPLATE=".env.example"
PLACEHOLDER_PREFIX="change-me-"

# Secrets in .env: base64 values, except the S3 credentials, which must be hex to fit in a connection string.
BASE64_SECRETS=(POSTGRES_PASSWORD INTERNAL_API_KEY LLM_API_KEY SEARXNG_SECRET)
declare -A HEX_SECRET_BYTES=([STORAGE_ACCESS_KEY]=20 [STORAGE_SECRET_KEY]=32)

usage() {
  echo "Usage: $0 [--skip-install]" >&2
  exit 2
}

skip_install=false
while [[ $# -gt 0 ]]; do
  case $1 in
    --skip-install) skip_install=true ;;
    *) usage ;;
  esac
  shift
done

cd "$REPO_ROOT"

check_prerequisites() {
  step "Checking prerequisites"
  local missing=0

  if has dotnet; then
    local required_sdk
    required_sdk=$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json)
    if dotnet --list-sdks | grep -q "^${required_sdk%.*}"; then
      ok ".NET SDK (global.json asks for $required_sdk, roll-forward latestFeature)"
    else
      warn ".NET SDK ${required_sdk} (or a later feature band) not found. Installed: $(dotnet --list-sdks | cut -d' ' -f1 | tr '\n' ' ')"
      missing=1
    fi
  else
    warn ".NET SDK not found: https://dotnet.microsoft.com/download"
    missing=1
  fi

  if dotnet ef --version >/dev/null 2>&1; then ok "dotnet-ef"; else warn "dotnet-ef missing (only needed for migrations): dotnet tool install --global dotnet-ef"; fi
  if has node; then ok "Node $(node --version)"; else warn "Node.js not found"; missing=1; fi
  if has pnpm; then ok "pnpm $(pnpm --version)"; else warn "pnpm not found: corepack enable pnpm"; missing=1; fi
  if has openssl; then ok "openssl"; else warn "openssl not found (needed to generate secrets)"; missing=1; fi

  if ! has docker; then
    warn "Docker not found"
    missing=1
  elif docker info >/dev/null 2>&1; then
    ok "Docker (daemon running)"
  else
    warn "Docker is installed but the daemon is not running"
  fi

  if has cargo; then ok "Rust (Satelles)"; else warn "Rust not found (only needed for Sentium Satelles): https://rustup.rs"; fi

  [[ $missing -eq 0 ]] || die "Install the missing prerequisites above, then re-run."
}

# Replaces "KEY=" or "KEY=change-me-..." with "KEY=<value>", leaving any other value alone.
fill_secret() {
  local key=$1 value=$2
  if grep -Eq "^${key}=(${PLACEHOLDER_PREFIX}.*)?$" "$ENV_FILE"; then
    # "|" is the sed delimiter: base64 output never contains it.
    sed -i.bak -E "s|^${key}=(${PLACEHOLDER_PREFIX}.*)?$|${key}=${value}|" "$ENV_FILE"
    rm -f "$ENV_FILE.bak"
    ok "Generated $key"
  fi
}

create_env() {
  step "Configuring $ENV_FILE"
  if [[ -f $ENV_FILE ]]; then
    ok "$ENV_FILE exists, keeping its values"
  else
    cp "$ENV_TEMPLATE" "$ENV_FILE"
    ok "Copied $ENV_TEMPLATE to $ENV_FILE"
  fi

  local key
  for key in "${BASE64_SECRETS[@]}"; do
    fill_secret "$key" "$(openssl rand -base64 32)"
  done
  for key in "${!HEX_SECRET_BYTES[@]}"; do
    fill_secret "$key" "$(openssl rand -hex "${HEX_SECRET_BYTES[$key]}")"
  done
}

install_js_dependencies() {
  step "pnpm install (whole workspace)"
  pnpm install --frozen-lockfile
}

check_prerequisites
create_env
[[ $skip_install == true ]] || install_js_dependencies

cat <<EOF

${_bold}Next steps${_reset}
  Dev stack (Aspire):   dotnet run --project src/aspire/Sentium.AppHost
  Compose stack:        docker compose up --build -d
                        (first run downloads ~5 GB of models; follow with: docker compose logs -f models-init llm)
  Trust the LAN cert:   scripts/trust-lan-cert.sh   (after the compose stack has started once)
  Check before a push:  scripts/verify.sh
EOF
