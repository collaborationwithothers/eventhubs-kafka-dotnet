#!/usr/bin/env bash
# Stop hook enforcing AGENTS.md GOVERNANCE "Handoffs" in unattended runs.
#
# Blocks the stop unless the session's issue, or the open pull request for its
# branch, has a comment starting with the marker below that was created since
# the session started. After MAX_BLOCKS blocks in a session, or on any error
# the agent cannot fix, it allows the stop and says why. The cap is this
# hook's own: Claude Code's cap of 8 resets on every tool call, so a session
# whose posting keeps failing would otherwise loop.
#
# Sources for the hook contract (Stop input, decision/reason, systemMessage,
# block cap, CLAUDE_PROJECT_DIR, inherited environment):
#   https://code.claude.com/docs/en/hooks
#   https://code.claude.com/docs/en/hooks-guide ("Stop hook hits the block cap")
# claude-code-action at commit 1d6de8cb0c237e7c15e9e1bdf973826ebae490cc: new branches
# are ${branchPrefix}${entityType}-${entityNumber}-${timestamp} (src/utils/branch-template.ts),
# branch_prefix defaults to "claude/" (action.yml), and entityType is "pr" or "issue"
# (src/github/operations/branch.ts). An open pull request's own head branch is reused,
# whatever its name, so the hook also looks up the branch's open pull request.
#   https://github.com/anthropics/claude-code-action/blob/1d6de8cb0c237e7c15e9e1bdf973826ebae490cc/src/utils/branch-template.ts
#   https://github.com/anthropics/claude-code-action/blob/1d6de8cb0c237e7c15e9e1bdf973826ebae490cc/action.yml
#   https://github.com/anthropics/claude-code-action/blob/1d6de8cb0c237e7c15e9e1bdf973826ebae490cc/src/github/operations/branch.ts
set -uo pipefail
# shellcheck source=SCRIPTDIR/handoff-lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/handoff-lib.sh"

readonly MARKER='**Decision needed:**'
readonly MARKER_REGEX='^\s*\*\*Decision needed:\*\*'
readonly PAGE_SIZE=100 # GitHub's documented maximum for "List issue comments"
readonly MAX_PAGES=50
readonly MAX_BLOCKS=3

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

session_id="$(jq -r '.session_id // empty' 2>/dev/null)"
project_dir="$(handoff_project_dir)"

# Target from the workflow variable or a numbered branch name, if either gives one.
issue="" kind="issue"
branch="$(git -C "$project_dir" branch --show-current 2>/dev/null)"
if [[ -n "${PARCHWELL_ISSUE:-}" ]]; then
  [[ "$PARCHWELL_ISSUE" =~ ^[0-9]+$ ]] || warn "PARCHWELL_ISSUE is not an issue number, so the handoff was not checked."
  issue="$PARCHWELL_ISSUE"
# Accepts claude/<n>-... and claude-code-action's claude/issue-<n>-... and claude/pr-<n>-...
elif [[ "$branch" =~ ^claude/((issue|pr)-)?([0-9]+)- ]]; then
  issue="${BASH_REMATCH[3]}"
  [[ "${BASH_REMATCH[2]}" == "pr" ]] && kind="pull request"
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

# Open pull requests whose head is this branch, whatever the branch is called.
prs=""
if [[ -n "$branch" ]]; then
  prs="$(gh api -X GET "repos/$repo/pulls" -f head="${repo%%/*}:$branch" -f state=open --jq '.[].number')" \
    || warn "GitHub API call failed, so the handoff was not checked."
fi

if [[ -z "$issue" ]]; then
  read -r issue _ <<<"$prs"
  kind="pull request"
  [[ -n "$issue" ]] || warn "no issue found from PARCHWELL_ISSUE, branch '$branch' or an open pull request, so the handoff was not checked."
fi

# Prints one line per marked comment created since the session started.
# Pages by number: AGENTS.md forbids gh api --paginate, whose next-page URLs
# the session proxy refuses.
marked_comments() { # issue_or_pr_number
  local page json
  for ((page = 1; page <= MAX_PAGES; page++)); do
    json="$(gh api -X GET "repos/$repo/issues/$1/comments" -f since="$started" \
      -f per_page="$PAGE_SIZE" -f page="$page")" || return 1
    jq -r --arg s "$started" --arg re "$MARKER_REGEX" \
      '.[] | select(.created_at >= $s) | select(.body | test($re)) | .created_at' <<<"$json" || return 1
    (( $(jq length <<<"$json") < PAGE_SIZE )) && return 0
  done
  return 1 # More pages than MAX_PAGES: report as a failed check.
}

found="$(marked_comments "$issue")" || warn "GitHub API call failed, so the handoff was not checked."
targets="$kind #$issue"
for pr in $prs; do
  [[ "$pr" == "$issue" ]] && continue # already counted above
  found+=$'\n'"$(marked_comments "$pr")" || warn "GitHub API call failed, so the handoff was not checked."
  targets+=" or pull request #$pr"
done

count="$(grep -c . <<<"$found")"

if [[ "$count" -eq 1 ]]; then
  allow
elif [[ "$count" -gt 1 ]]; then
  warn "$count handoff comments since the session started on $targets; GOVERNANCE requires exactly one."
fi

# Count this hook's own blocks for the session. It does not use stop_hook_active.
blocks_file="$state_file.blocks"
blocks="$(cat "$blocks_file" 2>/dev/null)"
[[ "$blocks" =~ ^[0-9]+$ ]] || blocks=0
if (( blocks >= MAX_BLOCKS )); then
  warn "session ended without a handoff comment on $targets after $MAX_BLOCKS reminders."
fi
echo $((blocks + 1)) >"$blocks_file"

block "GOVERNANCE \"Handoffs\": before stopping, post exactly one comment on $targets that starts with $MARKER and states, in one sentence, the decision you need from Hari, with the evidence linked. Write the comment to a file, then post it with: gh api -X POST repos/$repo/issues/$issue/comments -F body=@<file>"
