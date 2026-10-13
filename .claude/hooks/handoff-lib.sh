# shellcheck shell=bash
# Shared by handoff-session-start.sh and require-handoff.sh. Sourced, not run.

# Enforced in GitHub Actions, or when the session was launched with
# PARCHWELL_HANDOFF=enforce. Hooks inherit Claude Code's parent environment,
# so a variable exported in the Bash tool is not expected to reach them.
handoff_enforced() {
  [[ "${GITHUB_ACTIONS:-}" == "true" || "${PARCHWELL_HANDOFF:-}" == "enforce" ]]
}

handoff_project_dir() {
  printf '%s' "${CLAUDE_PROJECT_DIR:-$PWD}"
}

# Session IDs become file names, so accept only a safe character set.
handoff_valid_session_id() {
  [[ "$1" =~ ^[A-Za-z0-9_-]+$ ]]
}

# State lives under the git directory so it never appears as an untracked file.
handoff_state_file() { # session_id
  local git_dir
  git_dir="$(git -C "$(handoff_project_dir)" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || return 1
  printf '%s/parchwell-handoff/%s' "$git_dir" "$1"
}
