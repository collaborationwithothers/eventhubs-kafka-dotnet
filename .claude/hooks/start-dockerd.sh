#!/usr/bin/env bash
# SessionStart hook: starts the Docker daemon in the background in Claude Code
# cloud sessions, so the Docker-tier tests can run there. Never blocks or fails
# a session start: it always exits 0 and prints nothing, because a SessionStart
# hook's plain-text stdout is added to Claude's context.
# It does not wait for the daemon to become ready.
# Hooks reference: https://code.claude.com/docs/en/hooks
set -uo pipefail

[[ "${CLAUDE_CODE_REMOTE:-}" == "true" ]] || exit 0
[[ "$(id -u 2>/dev/null)" == "0" ]] || exit 0
command -v dockerd >/dev/null 2>&1 || exit 0
command -v pgrep >/dev/null 2>&1 || exit 0
pgrep -x dockerd >/dev/null 2>&1 && exit 0

log_file="${TMPDIR:-/tmp}/parchwell-dockerd.log"
# Detached: its own session, no inherited stdin or output, so it outlives the hook.
if command -v setsid >/dev/null 2>&1; then
    setsid dockerd </dev/null >>"$log_file" 2>&1 &
else
    nohup dockerd </dev/null >>"$log_file" 2>&1 &
fi
exit 0
