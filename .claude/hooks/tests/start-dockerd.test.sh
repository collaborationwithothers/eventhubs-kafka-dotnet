#!/usr/bin/env bash
# Tests for the start-dockerd.sh SessionStart hook.
# Run: .claude/hooks/tests/start-dockerd.test.sh
# Stub dockerd, pgrep and id commands replace the real ones: no Docker and no
# network are used, and no real daemon is started.
set -uo pipefail

HOOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/start-dockerd.sh"
BASH_BIN="$(command -v bash)"
WORK="$(mktemp -d)"
STUB_BIN="$WORK/bin"
mkdir -p "$STUB_BIN"

failed_cases=()
cases=0

cat >"$STUB_BIN/dockerd" <<'STUB'
#!/usr/bin/env bash
echo "$# $*" >>"$FAKE_CALLS"
echo $$ >"$FAKE_PID_FILE"
echo stub-dockerd-out
echo stub-dockerd-err >&2
[[ "${FAKE_DOCKERD_FAILS:-}" == "1" ]] && exit 1
exec sleep 30
STUB
cat >"$STUB_BIN/pgrep" <<'STUB'
#!/usr/bin/env bash
[[ "${FAKE_DOCKERD_RUNNING:-}" == "1" ]]
STUB
cat >"$STUB_BIN/id" <<'STUB'
#!/usr/bin/env bash
echo "${FAKE_UID:-0}"
STUB
chmod +x "$STUB_BIN"/*

# Directory of links to the real tools the hook may use besides the stubs.
TOOLS="$WORK/tools"
mkdir -p "$TOOLS"
for t in bash setsid nohup sleep; do
    p="$(command -v "$t" 2>/dev/null)" && ln -s "$p" "$TOOLS/$t"
done

# PATH variants. "without X" holds the stubs except X, plus the tools.
make_path_without() {
    local missing="$1" d="$WORK/path-without-$1"
    mkdir -p "$d"
    for s in dockerd pgrep id; do
        [[ "$s" == "$missing" ]] || ln -sf "$STUB_BIN/$s" "$d/$s"
    done
    for t in "$TOOLS"/*; do ln -sf "$t" "$d/$(basename "$t")"; done
    echo "$d"
}

# run_hook <PATH value> <env assignments...>
# Sets: status, out, elapsed_ms, calls_count (the number of lines in the calls
# file right after the hook returns). It does not stop any stub dockerd; use
# stop_stub for that.
run_hook() {
    local path_value="$1"
    shift
    CASE_DIR="$(mktemp -d "$WORK/case.XXXXXX")"
    : >"$CASE_DIR/calls"
    local start end
    start=$(date +%s%N)
    out="$(env -i PATH="$path_value" TMPDIR="$CASE_DIR" FAKE_CALLS="$CASE_DIR/calls" \
        FAKE_PID_FILE="$CASE_DIR/pid" "$@" "$BASH_BIN" "$HOOK" </dev/null 2>"$CASE_DIR/stderr")"
    status=$?
    end=$(date +%s%N)
    elapsed_ms=$(((end - start) / 1000000))
    calls_count=$(wc -l <"$CASE_DIR/calls")
}

stop_stub() {
    if [[ -s "$CASE_DIR/pid" ]]; then
        kill "$(cat "$CASE_DIR/pid")" 2>/dev/null || true
    fi
}

check() {
    local name="$1"
    shift
    cases=$((cases + 1))
    if ! "$@"; then
        failed_cases+=("$name")
    fi
}

# A wrongly started daemon is detached, so give it time to leave a trace.
assert_silent_noop() {
    sleep 1
    calls_count=$(wc -l <"$CASE_DIR/calls")
    [[ $status -eq 0 && -z "$out" && $calls_count -eq 0 ]]
}

FULL_PATH="$STUB_BIN:$TOOLS:$(dirname "$BASH_BIN"):/usr/bin:/bin"

# Requirement 5.
run_hook "$FULL_PATH" FAKE_UID=0
check remote_unset assert_silent_noop
run_hook "$FULL_PATH" CLAUDE_CODE_REMOTE=false FAKE_UID=0
check remote_false assert_silent_noop
run_hook "$FULL_PATH" CLAUDE_CODE_REMOTE=TRUE FAKE_UID=0
check remote_upper assert_silent_noop

# Requirement 6.
run_hook "$FULL_PATH" CLAUDE_CODE_REMOTE=true FAKE_UID=1001
check not_root assert_silent_noop

# Requirement 7.
run_hook "$(make_path_without dockerd)" CLAUDE_CODE_REMOTE=true FAKE_UID=0
assert_no_dockerd() { sleep 1; [[ $status -eq 0 && -z "$out" && ! -e "$CASE_DIR/parchwell-dockerd.log" ]]; }
check no_dockerd assert_no_dockerd

# Requirement 8.
run_hook "$(make_path_without pgrep)" CLAUDE_CODE_REMOTE=true FAKE_UID=0
check no_pgrep assert_silent_noop
stop_stub

# Requirement 9.
run_hook "$FULL_PATH" CLAUDE_CODE_REMOTE=true FAKE_UID=0 FAKE_DOCKERD_RUNNING=1
check running assert_silent_noop

# Requirement 10.
run_hook "$FULL_PATH" CLAUDE_CODE_REMOTE=true FAKE_UID=0
assert_start() {
    [[ $status -eq 0 && -z "$out" ]] || return 1
    local i
    for i in $(seq 1 50); do [[ -s "$CASE_DIR/calls" ]] && break; sleep 0.1; done
    calls_count=$(wc -l <"$CASE_DIR/calls")
    [[ $calls_count -eq 1 && "$(cat "$CASE_DIR/calls")" == "0 " ]] || return 1
    [[ $elapsed_ms -lt 5000 ]] || return 1
    for i in $(seq 1 50); do [[ -s "$CASE_DIR/pid" ]] && break; sleep 0.1; done
    [[ -s "$CASE_DIR/pid" ]] && kill -0 "$(cat "$CASE_DIR/pid")" 2>/dev/null || return 1
    for i in $(seq 1 50); do
        grep -q stub-dockerd-out "$CASE_DIR/parchwell-dockerd.log" 2>/dev/null \
            && grep -q stub-dockerd-err "$CASE_DIR/parchwell-dockerd.log" 2>/dev/null && return 0
        sleep 0.1
    done
    return 1
}
check start assert_start
stop_stub

# Requirement 11.
run_hook "$FULL_PATH" CLAUDE_CODE_REMOTE=true FAKE_UID=0 FAKE_DOCKERD_FAILS=1
assert_fails() {
    local i
    for i in $(seq 1 50); do [[ $(wc -l <"$CASE_DIR/calls") -ge 1 ]] && break; sleep 0.1; done
    [[ $status -eq 0 && -z "$out" && $(wc -l <"$CASE_DIR/calls") -eq 1 ]]
}
check dockerd_fails assert_fails
stop_stub

rm -rf "$WORK"
if [[ ${#failed_cases[@]} -eq 0 ]]; then
    echo "start-dockerd.sh checks passed. ($cases cases)"
    exit 0
fi
echo "start-dockerd.sh checks failed: ${failed_cases[*]}" >&2
exit 1
