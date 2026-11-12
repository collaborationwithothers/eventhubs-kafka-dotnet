#!/usr/bin/env bash
# Regression checks for scripts/verify-dotnet.sh. They put a stub `dotnet` first on PATH, so no
# build or test runs and no SDK or Docker is needed. Run: ./scripts/test-verify-dotnet.sh
set -euo pipefail

repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
work=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-verify-dotnet-test.XXXXXXXX")
trap 'rm -rf "$work"' EXIT
mkdir "$work/bin"

cat > "$work/bin/dotnet" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_LOG"
case "$1" in
    --version) echo 10.0.402 ;;
    msbuild)
        for argument in "$@"; do
            case "$argument" in
                -p:ProjectList=*)
                    for suffix in Enrichment Kafka LoadGen Scenarios Verifier; do
                        echo "tests/Parchwell.$suffix.Tests/Parchwell.$suffix.Tests.csproj"
                    done > "${argument#-p:ProjectList=}"
                    ;;
                -t:ValidateReport) echo "  stub: checked report." ;;
            esac
        done
        ;;
    test)
        project=$3
        name=$(basename "${project%.csproj}")
        printf 'TEST %s %s\n' "$name" "$*" >> "$STUB_LOG"
        for failing in ${STUB_FAIL:-}; do
            if [[ "$failing" == "$name" ]]; then echo "stub failure in $name"; exit 2; fi
        done
        ;;
    build)
        if [[ "$*" == *Forbidden.cs* && "${STUB_ACCEPT_WARNING:-}" != 1 ]]; then
            echo "error CS1030: #warning: 'PARCHWELL_WARNING_CONTROL'"
            exit 1
        fi
        ;;
esac
exit 0
STUB
chmod +x "$work/bin/dotnet"

failures=0
fail() { echo "FAIL: $*" >&2; failures=$((failures + 1)); }

# run_case <name> [env assignments...] -- [script arguments...]; sets status, output and log.
run_case() {
    local case_name=$1
    shift
    local environment=()
    while (($# > 0)) && [[ "$1" != -- ]]; do environment+=("$1"); shift; done
    shift
    export STUB_LOG="$work/$case_name.log"
    : > "$STUB_LOG"
    status=0
    env PATH="$work/bin:$PATH" TMPDIR="$work" ${environment[@]+"${environment[@]}"} \
        "$repository_root/scripts/verify-dotnet.sh" "$@" > "$work/$case_name.out" 2>&1 || status=$?
    output=$(<"$work/$case_name.out")
    log=$(<"$STUB_LOG")
    tested=$(grep -c '^TEST ' "$STUB_LOG" || true)
}

run_case default --
[[ $status -eq 0 ]] || fail "default run exited $status"
[[ $tested -eq 6 ]] || fail "default run invoked $tested test projects, expected 6"
[[ "$log" != *filter-not-trait* ]] || fail "default run must not filter tests"
[[ "$output" == *"Verification passed. Per-project reports"* ]] || fail "default run must print the full-gate pass line"

run_case fast -- --tier fast
[[ $status -eq 0 ]] || fail "fast tier exited $status"
[[ $tested -eq 6 ]] || fail "fast tier invoked $tested test projects, expected 6"
[[ $(grep -c '^TEST .*--filter-not-trait Tier=Docker Tier=Slow Tier=Host' "$STUB_LOG") -eq 6 ]] \
    || fail "every fast-tier test run must exclude Tier=Docker, Tier=Slow and Tier=Host"
[[ "$output" == *"Partial verification passed"* && "$output" != *"Verification passed. Per-project reports"* ]] \
    || fail "fast tier must say it is not the full gate"

run_case fail_late STUB_FAIL="Parchwell.Kafka.Tests Parchwell.Verifier.Tests" --
[[ $status -eq 2 ]] || fail "fail-late run exited $status, expected the first failure's status 2"
[[ $tested -eq 6 ]] || fail "fail-late run invoked $tested test projects, expected all 6 despite two failures"
[[ "$output" == *"Verification failed in 2 stage(s)"* && "$output" == *"Parchwell.Kafka.Tests (exit 2)"* && "$output" == *"Parchwell.Verifier.Tests (exit 2)"* ]] \
    || fail "fail-late run must list both failed projects"
[[ "$log" == *"build"*"Forbidden.cs"* ]] || fail "fail-late run must still reach the warning control"

run_case select -- --project Parchwell.Scenarios.Tests
[[ $status -eq 0 && $tested -eq 1 && "$log" == *"TEST Parchwell.Scenarios.Tests"* ]] || fail "--project must run only the named project (status $status, ran $tested)"
[[ "$output" == *"Partial verification passed"* ]] || fail "--project must say it is not the full gate"

run_case unknown_project -- --project Parchwell.Nope
[[ $status -eq 2 && $tested -eq 0 ]] || fail "unknown --project must exit 2 before testing (status $status, ran $tested)"
run_case unknown_tier -- --tier slow
[[ $status -eq 2 && $tested -eq 0 ]] || fail "unknown --tier must exit 2 before testing (status $status, ran $tested)"
run_case bad_cpu PARCHWELL_BUILD_MAX_CPU=0 --
[[ $status -eq 2 && $tested -eq 0 ]] || fail "invalid PARCHWELL_BUILD_MAX_CPU must exit 2 (status $status, ran $tested)"

run_case cpu PARCHWELL_BUILD_MAX_CPU=3 --
[[ $status -eq 0 ]] || fail "cpu-capped run exited $status"
grep -q '^restore Parchwell.slnx .*-maxcpucount:3' "$STUB_LOG" || fail "restore must receive -maxcpucount:3"
grep -q '^build Parchwell.slnx .*-maxcpucount:3' "$STUB_LOG" || fail "build must receive -maxcpucount:3"
if grep '^TEST ' "$STUB_LOG" | grep -q maxcpucount; then fail "test runs must not receive -maxcpucount"; fi

run_case warning_accepted STUB_ACCEPT_WARNING=1 --
[[ $status -ne 0 && $tested -eq 6 ]] || fail "an accepted intentional warning must fail the run after all tests (status $status, ran $tested)"
[[ "$output" == *"Warning control (exit"* ]] || fail "the failed warning control must be listed"

if [[ $failures -gt 0 ]]; then
    echo "$failures check(s) failed. Outputs: $work" >&2
    trap - EXIT
    exit 1
fi
echo "verify-dotnet.sh checks passed."
