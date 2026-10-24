#!/bin/bash
set -euo pipefail
umask 077
trap 'printf "%s:%s: exit %s\n" "${BASH_SOURCE[0]}" "$LINENO" "$?" >&2' ERR

PARCHWELL_LIFECYCLE_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PARCHWELL_REPOSITORY="$(cd "$PARCHWELL_LIFECYCLE_DIRECTORY/../../.." && pwd)"
export PARCHWELL_LIFECYCLE_DIRECTORY PARCHWELL_REPOSITORY

run_command() {
    local seconds="$1"
    shift
    python3 "$PARCHWELL_LIFECYCLE_DIRECTORY/command.py" "$seconds" -- "$@"
}

capture_command() {
    local seconds="$1" destination="$2" maximum="$3"
    shift 3
    python3 "$PARCHWELL_LIFECYCLE_DIRECTORY/command.py" "$seconds" \
        --stdout-file "$destination" --max-bytes "$maximum" -- "$@"
}
