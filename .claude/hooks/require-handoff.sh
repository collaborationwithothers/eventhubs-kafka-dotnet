#!/usr/bin/env bash
# Stop hook enforcing AGENTS.md GOVERNANCE "Handoffs" in unattended runs.
#
# Blocks the stop once unless the session's issue, or the open pull request
# for its branch, has a comment starting with the marker below that was
# created since the session started. If the agent stops again right after
# this hook's own block, or on any error the agent cannot fix, it allows the
# stop and says why, so it cannot loop.
#
# Sources for the hook contract (Stop input, decision/reason, systemMessage,
# stop_hook_active, CLAUDE_PROJECT_DIR, inherited environment):
#   https://code.claude.com/docs/en/hooks
#   https://code.claude.com/docs/en/hooks-guide ("Stop hook hits the block cap")
# claude-code-action branch names (main branch, unpinned, read 2026-10-13): template ${branchPrefix}${entityType}-${entityNumber}-${timestamp}
# (src/utils/branch-template.ts), branch_prefix default "claude/" (action.yml), entityType
# "pr" or "issue" (src/github/operations/branch.ts). An open pull request's own head
# branch is reused instead, so claude/pr-<n>-... appears only for closed or merged ones.
#   https://github.com/anthropics/claude-code-action/blob/main/src/utils/branch-template.ts
#   https://github.com/anthropics/claude-code-action/blob/main/action.yml
#   https://github.com/anthropics/claude-code-action/blob/main/src/github/operations/branch.ts
set -uo pipefail
# shellcheck source=SCRIPTDIR/handoff-lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/handoff-lib.sh"

readonly MARKER='**Decision needed:**'
readonly MARKER_REGEX='^\\s*\\*\\*Decision needed:\\*\\*'

allow() { exit 0; }

warn() { # message
  jq -n --arg m "Handoff check: $1" '{systemMessage: $m}'
  exit 0
}

block() { # reason
  jq -n --arg r "$1" '{decision: "block", reason: $r}'
  exit 0
}

handoff_enforced || allow

if ! command -v jq >/dev/null 2>&1; then
  printf '{"systemMessage": "Handoff check: jq is not installed, so the handoff was not checked."}\n'
  exit 0
fi

input="$(cat)"
session_id="$(jq -r '.session_id // empty' <<<"$input" 2>/dev/null)"
stop_hook_active="$(jq -r '.stop_hook_active // false' <<<"$input" 2>/dev/null)"
project_dir="$(handoff_project_dir)"

# Resolve the target issue: workflow variable first, then the branch name.
kind="issue"
branch="$(git -C "$project_dir" branch --show-current 2>/dev/null)"
if [[ -n "${PARCHWELL_ISSUE:-}" ]]; then
  [[ "$PARCHWELL_ISSUE" =~ ^[0-9]+$ ]] || warn "PARCHWELL_ISSUE is not an issue number, so the handoff was not checked."
  issue="$PARCHWELL_ISSUE"
# Accepts claude/<n>-... and claude-code-action's claude/issue-<n>-... and claude/pr-<n>-...
elif [[ "$branch" =~ ^claude/((issue|pr)-)?([0-9]+)- ]]; then
  issue="${BASH_REMATCH[3]}"
  [[ "${BASH_REMATCH[2]}" == "pr" ]] && kind="pull request"
else
  warn "no issue found from PARCHWELL_ISSUE or branch '$branch', so the handoff was not checked."
fi

handoff_valid_session_id "$session_id" || warn "the session id is missing or unsafe, so the handoff was not checked."
state_file="$(handoff_state_file "$session_id")" || warn "no git directory found, so the handoff was not checked."
[[ -r "$state_file" ]] || warn "the session start time was not recorded, so the handoff was not checked."
started="$(<"$state_file")"
[[ "$started" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$ ]] || warn "the recorded session start time is malformed."

# Resolve owner/repo. REST only: GraphQL is unavailable from some sessions.
repo="${GITHUB_REPOSITORY:-}"
if [[ -z "$repo" ]]; then
  remote="$(git -C "$project_dir" remote get-url origin 2>/dev/null)"
  if [[ "$remote" =~ github\.com[:/]([^/]+)/([^/]+)$ ]]; then
    repo="${BASH_REMATCH[1]}/${BASH_REMATCH[2]%.git}"
  fi
fi
[[ "$repo" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || warn "could not determine the GitHub repository, so the handoff was not checked."

command -v gh >/dev/null 2>&1 || warn "gh is not installed, so the handoff was not checked."

# Prints one line per marked comment created since the session started.
marked_comments() { # issue_or_pr_number
  local filter
  filter=".[] | select(.created_at >= \"$started\") | select(.body | test(\"$MARKER_REGEX\")) | .created_at"
  gh api --paginate -X GET "repos/$repo/issues/$1/comments" -f since="$started" --jq "$filter"
}

found="$(marked_comments "$issue")" || warn "GitHub API call failed, so the handoff was not checked."
targets="$kind #$issue"

if [[ -n "$branch" ]]; then
  prs="$(gh api -X GET "repos/$repo/pulls" -f head="${repo%%/*}:$branch" -f state=open --jq '.[].number')" \
    || warn "GitHub API call failed, so the handoff was not checked."
  for pr in $prs; do
    [[ "$pr" == "$issue" ]] && continue # already counted above
    found+=$'\n'"$(marked_comments "$pr")" || warn "GitHub API call failed, so the handoff was not checked."
    targets+=" or pull request #$pr"
  done
fi

count="$(grep -c . <<<"$found")"

# Inferred, not stated in the docs: stop_hook_active may also be true after
# another Stop hook's block. So this hook remembers its own block and allows
# only when it has already blocked once.
blocked_file="$state_file.blocked"
if [[ "$count" -ge 1 || "$stop_hook_active" != "true" ]]; then
  rm -f "$blocked_file"
fi

if [[ "$count" -eq 1 ]]; then
  allow
elif [[ "$count" -gt 1 ]]; then
  warn "$count handoff comments since the session started on $targets; GOVERNANCE requires exactly one."
elif [[ -e "$blocked_file" ]]; then
  rm -f "$blocked_file"
  warn "session ended without a handoff comment on $targets."
fi

: >"$blocked_file"

block "GOVERNANCE \"Handoffs\": before stopping, post exactly one comment on $targets that starts with $MARKER and states, in one sentence, the decision you need from Hari, with the evidence linked. Use gh api REST, for example: gh api repos/$repo/issues/$issue/comments -f body='$MARKER ...'"
