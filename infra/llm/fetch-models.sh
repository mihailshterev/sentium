#!/bin/sh
# Downloads every model file listed in models.lock into $MODELS_DIR and verifies its SHA-256.
#
# - Idempotent: a file that is already present with the right checksum is skipped.
# - Resumable: an interrupted download continues from <file>.part on the next run.
# - Atomic: a file only appears under its final name after its checksum has been verified,
#   so the inference servers never see a partial or tampered model.
# - Exits non-zero if any file cannot be downloaded or fails verification after $MAX_ATTEMPTS tries.
#
# Optional HF_TOKEN is sent as a bearer token (needed only for gated repositories).
# Runs in a minimal image with POSIX sh, curl and sha256sum (curlimages/curl).
set -eu

LOCK_FILE="${LOCK_FILE:-/infra/models.lock}"
MODELS_DIR="${MODELS_DIR:-/models}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-5}"

mkdir -p "$MODELS_DIR"

verify() {
    echo "$2  $1" | sha256sum -c -s -
}

download() { # url output
    if [ -n "${HF_TOKEN:-}" ]; then
        curl --fail --location --silent --show-error --retry 3 --continue-at - \
            --header "Authorization: Bearer ${HF_TOKEN}" --output "$2" "$1"
    else
        curl --fail --location --silent --show-error --retry 3 --continue-at - \
            --output "$2" "$1"
    fi
}

fetch() { # sha file url
    target="$MODELS_DIR/$2"
    part="$target.part"

    if [ -f "$target" ] && verify "$target" "$1"; then
        echo "ok        $2 (already present)"
        return 0
    fi
    rm -f "$target"

    attempt=1
    while [ "$attempt" -le "$MAX_ATTEMPTS" ]; do
        if [ -f "$part" ] && verify "$part" "$1"; then
            mv "$part" "$target"
            echo "ok        $2"
            return 0
        fi

        echo "download  $2 (attempt $attempt/$MAX_ATTEMPTS)"
        download "$3" "$part" &
        pid=$!
        while kill -0 "$pid" 2>/dev/null; do
            sleep 15
            if [ -f "$part" ]; then
                echo "          $2: $(du -m "$part" | cut -f1) MiB"
            fi
        done

        if wait "$pid"; then
            if verify "$part" "$1"; then
                mv "$part" "$target"
                echo "ok        $2"
                return 0
            fi
            # A complete transfer with the wrong checksum cannot be resumed into a good file.
            echo "          $2: checksum mismatch; discarding the download"
            rm -f "$part"
        else
            echo "          $2: transfer interrupted; resuming"
        fi
        attempt=$((attempt + 1))
        sleep $((attempt * 5))
    done

    rm -f "$part"
    echo "FAILED    $2" >&2
    return 1
}

status=0
while read -r sha file url; do
    case "$sha" in
        '' | '#'*) continue ;;
    esac
    fetch "$sha" "$file" "$url" || status=1
done < "$LOCK_FILE"

if [ "$status" -eq 0 ]; then
    echo "All models ready."
fi
exit "$status"
