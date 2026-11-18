#!/usr/bin/env bash
# Adds an EF Core migration to one module's DbContext, then runs MigrationSnapshotTests so model drift
# in any other module shows up straight away.
#
#   scripts/add-migration.sh <Module> <MigrationName> [--no-test]
#   scripts/add-migration.sh --list
#
# The startup project must be Sentium.Web: a class-library startup project fails with a
# System.Runtime load error.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

STARTUP_PROJECT="src/hosts/Sentium.Web"
UNIT_TESTS="tests/Sentium.Tests.Unit"

usage() {
  echo "Usage: $0 <Module> <MigrationName> [--no-test]" >&2
  echo "       $0 --list" >&2
  exit 2
}

# Modules that own a DbContext, by folder name (src/modules/<Module>/Data/<Module>DbContext.cs).
modules_with_context() {
  local context
  for context in "$REPO_ROOT"/src/modules/*/Data/*DbContext.cs; do
    basename "$(dirname "$(dirname "$context")")"
  done
}

if [[ ${1:-} == "--list" ]]; then
  modules_with_context
  exit 0
fi

[[ $# -ge 2 ]] || usage
requested_module=$1
migration_name=$2
shift 2

run_tests=true
while [[ $# -gt 0 ]]; do
  case $1 in
    --no-test) run_tests=false ;;
    *) usage ;;
  esac
  shift
done

[[ $migration_name =~ ^[A-Z][A-Za-z0-9]*$ ]] \
  || die "Migration name must be PascalCase letters and digits, got '$migration_name'."

module=""
while read -r candidate; do
  if [[ ${candidate,,} == "${requested_module,,}" ]]; then
    module=$candidate
  fi
done < <(modules_with_context)

[[ -n $module ]] || die "No module with a DbContext named '$requested_module'. Choose one of: $(modules_with_context | tr '\n' ' ')"

has dotnet || die "The .NET SDK is not on PATH."
dotnet ef --version >/dev/null 2>&1 \
  || die "dotnet-ef is missing. Install it with: dotnet tool install --global dotnet-ef"

cd "$REPO_ROOT"

step "Adding migration $migration_name to ${module}DbContext"
dotnet ef migrations add "$migration_name" \
  --project "src/modules/$module" \
  --startup-project "$STARTUP_PROJECT" \
  --context "${module}DbContext" \
  --output-dir Data/Migrations

ok "Created src/modules/$module/Data/Migrations/*_$migration_name.cs"
echo "Review the generated Up/Down methods before committing."

if [[ $run_tests == true ]]; then
  step "Checking for model drift (MigrationSnapshotTests)"
  dotnet test "$UNIT_TESTS" --filter "FullyQualifiedName~MigrationSnapshotTests"
fi
