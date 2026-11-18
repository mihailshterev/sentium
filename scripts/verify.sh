#!/usr/bin/env bash
# Runs locally what .github/workflows/ci.yml runs, so failures show up before a push.
#
#   scripts/verify.sh [--dotnet-only | --portal-only] [--integration] [--satelles]
#
# By default: the .NET build (zero warnings) + unit tests, and the portal's lint, build and tests.
#   --integration  also run Sentium.Tests.Integration and Sentium.Tests.AppHost (need Docker)
#   --satelles     also run cargo clippy and cargo test for Sentium Satelles (need Rust)
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

BUILD_CONFIGURATION="Release"
PORTAL_DIR="src/clients/sentium-portal"
SATELLES_TAURI_DIR="src/clients/sentium-satelles/src-tauri"

usage() {
  echo "Usage: $0 [--dotnet-only | --portal-only] [--integration] [--satelles]" >&2
  exit 2
}

run_dotnet=true
run_portal=true
run_integration=false
run_satelles=false
while [[ $# -gt 0 ]]; do
  case $1 in
    --dotnet-only) run_portal=false ;;
    --portal-only) run_dotnet=false ;;
    --integration) run_integration=true ;;
    --satelles) run_satelles=true ;;
    -h | --help) usage ;;
    *) usage ;;
  esac
  shift
done

[[ $run_dotnet == true || $run_portal == true ]] || die "--dotnet-only and --portal-only exclude each other."

cd "$REPO_ROOT"
started=$SECONDS

verify_dotnet() {
  has dotnet || die "The .NET SDK is not on PATH."

  step ".NET restore (locked mode)"
  dotnet restore Sentium.slnx --locked-mode

  step ".NET build ($BUILD_CONFIGURATION, warnings are errors)"
  dotnet build Sentium.slnx --no-restore --configuration "$BUILD_CONFIGURATION"

  step "Unit tests"
  dotnet test tests/Sentium.Tests.Unit --no-build --configuration "$BUILD_CONFIGURATION"

  if [[ $run_integration == true ]]; then
    docker info >/dev/null 2>&1 || die "Integration tests need a running Docker daemon."
    step "Integration tests"
    dotnet test tests/Sentium.Tests.Integration --no-build --configuration "$BUILD_CONFIGURATION"
    step "AppHost tests"
    dotnet test tests/Sentium.Tests.AppHost --no-build --configuration "$BUILD_CONFIGURATION"
  fi
}

verify_portal() {
  has pnpm || die "pnpm is not on PATH."

  step "Portal install (frozen lockfile)"
  (cd "$PORTAL_DIR" && pnpm install --frozen-lockfile)
  step "Portal lint"
  (cd "$PORTAL_DIR" && pnpm run lint)
  step "Portal build"
  (cd "$PORTAL_DIR" && pnpm run build)
  step "Portal tests"
  (cd "$PORTAL_DIR" && pnpm run test:run)
}

verify_satelles() {
  has cargo || die "cargo is not on PATH (install Rust via rustup)."

  step "Satelles clippy"
  (cd "$SATELLES_TAURI_DIR" && cargo clippy --locked -- -D warnings)
  step "Satelles tests"
  (cd "$SATELLES_TAURI_DIR" && cargo test --locked)
}

[[ $run_dotnet == true ]] && verify_dotnet
[[ $run_portal == true ]] && verify_portal
[[ $run_satelles == true ]] && verify_satelles

ok "All checks passed in $((SECONDS - started))s."
