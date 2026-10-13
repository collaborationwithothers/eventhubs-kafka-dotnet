#!/usr/bin/env bash
# SessionStart hook: records when the session started, so require-handoff.sh
# counts only handoff comments written by this session. Never blocks.
# Runs on every SessionStart source; resume and compact keep the first time.
# Hooks reference: https://code.claude.com/docs/en/hooks
set -uo pipefail
# shellcheck source=SCRIPTDIR/handoff-lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/handoff-lib.sh"

handoff_enforced || exit 0
command -v jq >/dev/null 2>&1 || exit 0

session_id="$(jq -r '.session_id // empty' 2>/dev/null)" || exit 0
handoff_valid_session_id "$session_id" || exit 0
state_file="$(handoff_state_file "$session_id")" || exit 0

[[ -e "$state_file" ]] && exit 0
mkdir -p "$(dirname "$state_file")" && date -u +%Y-%m-%dT%H:%M:%SZ >"$state_file"
exit 0
