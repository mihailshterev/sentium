#!/usr/bin/env bash
# Restores a Sentium backup into this Docker Compose installation. Run it from Linux, macOS or WSL, with the
# application containers stopped. See "Backups" in docs/installation.md.
#
#   infra/backup/restore.sh <archive> [--identity <age identity file>] [--replace]
#
# The archive is verified (checksums, database dump, Sentium version) before anything is written. The target
# must be empty - a freshly wiped installation - unless --replace is given, which discards the current data.
set -euo pipefail

usage() {
  echo "Usage: $0 <archive> [--identity <age identity file>] [--replace]" >&2
  exit 2
}

absolute() {
  echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

[[ $# -ge 1 ]] || usage
archive=$1
shift

identity=""
replace=()
while [[ $# -gt 0 ]]; do
  case $1 in
    --identity)
      [[ $# -ge 2 ]] || usage
      identity=$2
      shift 2
      ;;
    --replace)
      replace=(--replace)
      shift
      ;;
    *) usage ;;
  esac
done

[[ -f $archive ]] || { echo "No such archive: $archive" >&2; exit 1; }
archive=$(absolute "$archive")
if [[ -n $identity ]]; then
  [[ -f $identity ]] || { echo "No such identity file: $identity" >&2; exit 1; }
  identity=$(absolute "$identity")
fi

cd "$(dirname "$0")/../.."

# Everything that writes to the database, the blobs or the key ring must be down.
running=$(docker compose ps --status running --services | grep -Ex 'sentium-web|sandbox|backup|caddy' || true)
if [[ -n $running ]]; then
  echo "These containers are still running:" >&2
  echo "$running" | sed 's/^/  /' >&2
  echo "Stop them first: docker compose stop sentium-web sandbox backup caddy" >&2
  exit 1
fi

mounts=(-v "$(dirname "$archive"):/restore/archive:ro")
args=(restore "/restore/archive/$(basename "$archive")")
if [[ -n $identity ]]; then
  mounts+=(-v "$identity:/restore/identity:ro")
  args+=(--identity /restore/identity)
fi

# Starts postgres and storage (waiting for both to be healthy) and runs the restore in the sidecar image.
docker compose --profile restore run --rm "${mounts[@]}" backup-restore "${args[@]}" ${replace[@]+"${replace[@]}"}
