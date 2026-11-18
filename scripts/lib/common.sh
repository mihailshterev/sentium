# Shared helpers for the scripts in scripts/. Source it, don't run it.
# shellcheck shell=bash

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
export REPO_ROOT

if [[ -t 1 ]]; then
  _bold=$'\e[1m' _red=$'\e[31m' _green=$'\e[32m' _yellow=$'\e[33m' _reset=$'\e[0m'
else
  _bold="" _red="" _green="" _yellow="" _reset=""
fi

step() { echo "${_bold}==> $*${_reset}"; }
ok()   { echo "${_green}ok${_reset}    $*"; }
warn() { echo "${_yellow}warn${_reset}  $*" >&2; }
die()  { echo "${_red}error${_reset} $*" >&2; exit 1; }

has() { command -v "$1" >/dev/null 2>&1; }

# Prints the OS family: windows (Git Bash / MSYS / Cygwin), macos or linux.
os_family() {
  case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*) echo windows ;;
    Darwin) echo macos ;;
    *) echo linux ;;
  esac
}
