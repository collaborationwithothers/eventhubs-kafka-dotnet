#!/usr/bin/env bash
# Tests for the handoff hooks. Run: .claude/hooks/tests/require-handoff.test.sh
# Needs bash, git and jq. A stub replaces gh, so no network is used.
set -uo pipefail

HOOKS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STOP_HOOK="$HOOKS_DIR/require-handoff.sh"
START_HOOK="$HOOKS_DIR/handoff-session-start.sh"
SESSION_ID="11111111-2222-3333-4444-555555555555"
START="2026-10-13T10:00:00Z"

failures=0
passes=0

# --- test fixture ----------------------------------------------------------

setup() {
  WORK="$(mktemp -d)"
  REPO="$WORK/repo"
  FIXTURES="$WORK/fixtures"
  STUB_BIN="$WORK/bin"
  mkdir -p "$REPO" "$FIXTURES" "$STUB_BIN"

  git -C "$REPO" init -q -b main
  git -C "$REPO" -c user.email=t@example.com -c user.name=t commit -q --allow-empty -m init
  git -C "$REPO" remote add origin https://github.com/acme/widget.git
  git -C "$REPO" checkout -q -b claude/29-handoff

  echo '[]' >"$FIXTURES/issue_comments.json"
  echo '[]' >"$FIXTURES/pulls.json"
  echo '[]' >"$FIXTURES/pr_comments.json"
  : >"$WORK/gh-calls.log"

  # Stub gh: serves fixtures for the REST paths the hook uses and applies --jq.
  cat >"$STUB_BIN/gh" <<'STUB'
#!/usr/bin/env bash
echo "$*" >>"$GH_CALLS_LOG"
[[ "${FAKE_GH_FAIL:-}" == "1" ]] && { echo "gh: HTTP 401" >&2; exit 1; }
path="" expr="."
shift # "api"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --jq) expr="$2"; shift 2 ;;
    -f|-F|-X|-H) shift 2 ;;
    --paginate) shift ;;
    *) [[ -z "$path" ]] && path="$1"; shift ;;
  esac
done
case "$path" in
  repos/acme/widget/issues/29/comments*) file="issue_comments.json" ;;
  repos/acme/widget/issues/77/comments*) file="pr_comments.json" ;;
  repos/acme/widget/pulls*) file="pulls.json" ;;
  *) echo "stub gh: unexpected path $path" >&2; exit 1 ;;
esac
jq -r "$expr" "$FIXTURES/$file"
STUB
  chmod +x "$STUB_BIN/gh"
}

teardown() { rm -rf "$WORK"; }

write_start_file() {
  mkdir -p "$REPO/.git/parchwell-handoff"
  echo "$START" >"$REPO/.git/parchwell-handoff/$SESSION_ID"
}

comment() { # created_at body
  jq -n --arg c "$1" --arg b "$2" '{created_at: $c, body: $b}'
}

set_fixture() { # file json-objects...
  local file="$1"; shift
  if [[ $# -eq 0 ]]; then echo '[]' >"$FIXTURES/$file"; return; fi
  printf '%s\n' "$@" | jq -s '.' >"$FIXTURES/$file"
}

# run_stop [stop_hook_active] [extra env assignments...]
run_stop() {
  local active="${1:-false}"; shift || true
  local input
  input="$(jq -n --arg s "$SESSION_ID" --arg c "$REPO" --argjson a "$active" \
    '{session_id: $s, cwd: $c, hook_event_name: "Stop", stop_hook_active: $a}')"
  OUT="$(cd "$REPO" && env -i PATH="$STUB_BIN:$PATH" HOME="$HOME" \
    GH_CALLS_LOG="$WORK/gh-calls.log" FIXTURES="$FIXTURES" \
    CLAUDE_PROJECT_DIR="$REPO" "$@" "$STOP_HOOK" <<<"$input" 2>"$WORK/stderr")"
  STATUS=$?
}

# --- assertions ------------------------------------------------------------

pass() { passes=$((passes + 1)); }
fail() { failures=$((failures + 1)); echo "FAIL: $CURRENT: $1"; }

assert_status() { [[ "$STATUS" == "$1" ]] || fail "exit $STATUS, expected $1"; }
assert_no_output() { [[ -z "$OUT" ]] || fail "expected no output, got: $OUT"; }
assert_blocked() {
  [[ "$(jq -r '.decision // empty' <<<"$OUT" 2>/dev/null)" == "block" ]] || fail "expected block, got: $OUT"
}
assert_warns() { # substring
  local msg
  msg="$(jq -r '.systemMessage // empty' <<<"$OUT" 2>/dev/null)"
  [[ "$msg" == *"$1"* ]] || fail "expected systemMessage containing '$1', got: $OUT"
  [[ "$(jq -r '.decision // empty' <<<"$OUT" 2>/dev/null)" != "block" ]] || fail "warning must not block"
}
assert_gh_not_called() { [[ ! -s "$WORK/gh-calls.log" ]] || fail "gh was called: $(cat "$WORK/gh-calls.log")"; }

run_test() {
  CURRENT="$1"
  local before=$failures
  setup
  "$1"
  teardown
  [[ $failures -eq $before ]] && pass
}

# --- Stop hook cases -------------------------------------------------------

test_not_enforced_outside_actions() {
  write_start_file
  run_stop false
  assert_status 0; assert_no_output; assert_gh_not_called
}

test_enforced_by_opt_in_variable() {
  write_start_file
  run_stop false PARCHWELL_HANDOFF=enforce
  assert_status 0; assert_blocked
}

test_no_target_allows_stop_with_warning() {
  git -C "$REPO" checkout -q main
  write_start_file
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_warns "no issue"
}

test_unnumbered_branch_uses_its_open_pr() {
  git -C "$REPO" checkout -q -b feature/fix
  write_start_file
  set_fixture pulls.json '{"number": 77}'
  set_fixture pr_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** merge #77?')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
  grep -q 'issues/77/comments' "$WORK/gh-calls.log" || fail "pull request 77 was not checked"
}

test_unnumbered_branch_without_handoff_blocks_on_its_pr() {
  git -C "$REPO" checkout -q -b feature/fix
  write_start_file
  set_fixture pulls.json '{"number": 77}'
  run_stop false GITHUB_ACTIONS=true
  assert_blocked
  [[ "$(jq -r '.reason' <<<"$OUT")" == *"pull request #77"* ]] || fail "reason does not name pull request #77: $OUT"
}

test_action_issue_branch_is_parsed() {
  git -C "$REPO" checkout -q -b claude/issue-29-20261013-1400
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
  grep -q 'issues/29/comments' "$WORK/gh-calls.log" || fail "issue 29 was not checked"
}

test_action_pr_branch_is_parsed() {
  git -C "$REPO" checkout -q -b claude/pr-77-20261013-1400
  write_start_file
  set_fixture pr_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** merge #77?')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
  grep -q 'issues/77/comments' "$WORK/gh-calls.log" || fail "pull request 77 was not checked"
}

test_pr_branch_block_names_pull_request() {
  git -C "$REPO" checkout -q -b claude/pr-77-20261013-1400
  write_start_file
  run_stop false GITHUB_ACTIONS=true
  assert_blocked
  [[ "$(jq -r '.reason' <<<"$OUT")" == *"pull request #77"* ]] || fail "reason does not name pull request #77: $OUT"
}

test_issue_variable_overrides_branch() {
  git -C "$REPO" checkout -q main
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop false GITHUB_ACTIONS=true PARCHWELL_ISSUE=29
  assert_status 0; assert_no_output
}

test_invalid_issue_variable_fails_open() {
  write_start_file
  run_stop false GITHUB_ACTIONS=true PARCHWELL_ISSUE='29;rm'
  assert_status 0; assert_warns "PARCHWELL_ISSUE"; assert_gh_not_called
}

test_marked_comment_after_start_allows_stop() {
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
}

test_marked_comment_before_start_blocks() {
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T09:59:59Z '**Decision needed:** old session.')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_blocked
}

test_unmarked_comment_blocks() {
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z 'Build passed. Decision needed: nothing.')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_blocked
}

test_block_reason_names_the_issue_and_marker() {
  write_start_file
  run_stop false GITHUB_ACTIONS=true
  local reason
  reason="$(jq -r '.reason' <<<"$OUT")"
  [[ "$reason" == *"#29"* && "$reason" == *"**Decision needed:**"* ]] || fail "reason lacks issue or marker: $reason"
}

test_pr_comment_allows_stop() {
  write_start_file
  set_fixture pulls.json '{"number": 77}'
  set_fixture pr_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** merge #77?')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
  grep -q 'head=acme:claude/29-handoff' "$WORK/gh-calls.log" || fail "PR lookup did not filter by head branch"
}

test_target_equal_to_open_pr_is_counted_once() {
  write_start_file
  set_fixture pulls.json '{"number": 29}'
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop false GITHUB_ACTIONS=true PARCHWELL_ISSUE=29
  assert_status 0; assert_no_output
}

test_more_than_one_handoff_warns() {
  write_start_file
  set_fixture issue_comments.json \
    "$(comment 2026-10-13T10:05:00Z '**Decision needed:** first.')" \
    "$(comment 2026-10-13T10:06:00Z '**Decision needed:** second.')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_warns "exactly one"
}

test_blocks_three_times_then_allows_with_warning() {
  write_start_file
  run_stop false GITHUB_ACTIONS=true; assert_blocked
  run_stop true GITHUB_ACTIONS=true; assert_blocked
  run_stop true GITHUB_ACTIONS=true; assert_blocked
  run_stop true GITHUB_ACTIONS=true
  assert_status 0; assert_warns "without a handoff"
}

test_block_count_ignores_stop_hook_active() {
  write_start_file
  run_stop true GITHUB_ACTIONS=true; assert_blocked
  run_stop false GITHUB_ACTIONS=true; assert_blocked
  run_stop false GITHUB_ACTIONS=true; assert_blocked
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_warns "without a handoff"
}

test_second_stop_with_handoff_allows_silently() {
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop true GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
}

test_gh_failure_fails_open() {
  write_start_file
  run_stop false GITHUB_ACTIONS=true FAKE_GH_FAIL=1
  assert_status 0; assert_warns "GitHub"
}

test_missing_start_file_fails_open() {
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_warns "start time"; assert_gh_not_called
}

test_ssh_remote_is_parsed() {
  git -C "$REPO" remote set-url origin git@github.com:acme/widget.git
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop false GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
}

test_github_repository_variable_wins() {
  git -C "$REPO" remote set-url origin https://example.com/other/thing.git
  write_start_file
  set_fixture issue_comments.json "$(comment 2026-10-13T10:05:00Z '**Decision needed:** approve X.')"
  run_stop false GITHUB_ACTIONS=true GITHUB_REPOSITORY=acme/widget
  assert_status 0; assert_no_output
}

test_unsafe_session_id_fails_open() {
  SESSION_ID="../../etc"
  run_stop false GITHUB_ACTIONS=true
  SESSION_ID="11111111-2222-3333-4444-555555555555"
  assert_status 0; assert_warns "session"; assert_gh_not_called
}

# --- SessionStart hook cases -----------------------------------------------

run_start() { # extra env assignments...
  local input
  input="$(jq -n --arg s "$SESSION_ID" --arg c "$REPO" '{session_id: $s, cwd: $c, hook_event_name: "SessionStart", source: "startup"}')"
  OUT="$(cd "$REPO" && env -i PATH="$PATH" HOME="$HOME" CLAUDE_PROJECT_DIR="$REPO" "$@" "$START_HOOK" <<<"$input" 2>"$WORK/stderr")"
  STATUS=$?
}

test_start_records_time_when_enforced() {
  run_start GITHUB_ACTIONS=true
  assert_status 0; assert_no_output
  local file="$REPO/.git/parchwell-handoff/$SESSION_ID"
  [[ -f "$file" ]] || { fail "start file not written"; return; }
  grep -Eq '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$' "$file" || fail "bad timestamp: $(cat "$file")"
  [[ -z "$(git -C "$REPO" status --porcelain)" ]] || fail "start file shows in git status"
}

test_start_does_not_overwrite() {
  write_start_file
  run_start GITHUB_ACTIONS=true
  [[ "$(cat "$REPO/.git/parchwell-handoff/$SESSION_ID")" == "$START" ]] || fail "start time overwritten"
}

test_start_does_nothing_when_not_enforced() {
  run_start
  assert_status 0
  [[ ! -e "$REPO/.git/parchwell-handoff" ]] || fail "wrote state although not enforced"
}

# --- runner ----------------------------------------------------------------

for t in $(declare -F | awk '{print $3}' | grep '^test_'); do
  run_test "$t"
done
echo "$passes passed, $failures failed"
[[ $failures -eq 0 ]]
