#!/usr/bin/env bash
# Exports the root certificate of the caddy service's local CA and trusts it on this machine, so the portal
# at https://SENTIUM_HOSTNAME loads without a certificate warning. Other devices (phones, other PCs) still
# need the exported file installed by hand: see "LAN access" in docs/installation.md.
#
#   scripts/trust-lan-cert.sh [--export-only] [--machine] [--out <file>]
#
#   --export-only  write the certificate and print its fingerprint, don't install it
#   --machine      Windows only: install for all users (LocalMachine, needs an elevated shell)
#                  instead of the current user
#   --out <file>   where to write the certificate (default: ./sentium-root-ca.crt)
#
# Needs the stack to have started once (caddy creates its CA on first start).
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

CADDY_ROOT_CERT="/data/caddy/pki/authorities/local/root.crt"
CERT_NAME="Sentium Local CA"

usage() {
  echo "Usage: $0 [--export-only] [--machine] [--out <file>]" >&2
  exit 2
}

export_only=false
machine_store=false
out_file="$REPO_ROOT/sentium-root-ca.crt"
while [[ $# -gt 0 ]]; do
  case $1 in
    --export-only) export_only=true ;;
    --machine) machine_store=true ;;
    --out)
      [[ $# -ge 2 ]] || usage
      out_file=$2
      shift
      ;;
    *) usage ;;
  esac
  shift
done

has docker || die "docker is not on PATH."

cd "$REPO_ROOT"

step "Exporting the caddy root certificate"
# MSYS_NO_PATHCONV stops Git Bash from rewriting the container path into a Windows path.
MSYS_NO_PATHCONV=1 docker compose cp "caddy:$CADDY_ROOT_CERT" "$out_file" \
  || die "Could not copy the certificate. Has the stack (and the caddy service) started at least once?"
ok "Wrote $out_file"

if has openssl; then
  echo "SHA-256 fingerprint (compare it on each device before trusting the file):"
  openssl x509 -in "$out_file" -noout -fingerprint -sha256
fi

[[ $export_only == true ]] && exit 0

install_windows() {
  local windows_path
  windows_path=$(cygpath -w "$out_file")
  if [[ $machine_store == true ]]; then
    step "Installing into the LocalMachine Root store"
    certutil -addstore -f Root "$windows_path" \
      || die "certutil failed. Run this from an elevated shell, or drop --machine to install for the current user."
  else
    step "Installing into the current user's Root store (Windows asks you to confirm)"
    certutil -user -addstore -f Root "$windows_path"
  fi
  echo "Restart the browser. Edge, Chrome and Firefox use the Windows store."
}

install_macos() {
  step "Installing into the System keychain as trusted root (needs sudo)"
  sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain "$out_file"
}

install_linux() {
  if [[ -d /usr/local/share/ca-certificates ]] && has update-ca-certificates; then
    step "Installing into the Debian/Ubuntu system store (needs sudo)"
    sudo cp "$out_file" /usr/local/share/ca-certificates/sentium-root-ca.crt
    sudo update-ca-certificates
  elif [[ -d /etc/pki/ca-trust/source/anchors ]] && has update-ca-trust; then
    step "Installing into the Fedora/RHEL system store (needs sudo)"
    sudo cp "$out_file" /etc/pki/ca-trust/source/anchors/sentium-root-ca.crt
    sudo update-ca-trust
  else
    die "Unknown Linux trust store. Install $out_file by hand (see docs/installation.md)."
  fi

  if has certutil && [[ -d $HOME/.pki/nssdb ]]; then
    step "Adding to Chrome's NSS database"
    certutil -d "sql:$HOME/.pki/nssdb" -A -t "C,," -n "$CERT_NAME" -i "$out_file"
  else
    warn "Chrome and Firefox on Linux keep their own lists; import the file there too (see docs/installation.md)."
  fi
}

case "$(os_family)" in
  windows) install_windows ;;
  macos) install_macos ;;
  linux) install_linux ;;
esac

ok "Trusted. Open https://<SENTIUM_HOSTNAME> to check."
