#!/usr/bin/env bash
# Regression checks for scripts/verify-dotnet.sh. They put a stub `dotnet` first on PATH, so no
# build or test runs and no SDK or Docker is needed. They read summary.json with jq, which must be
# installed. Run: ./scripts/test-verify-dotnet.sh
set -euo pipefail

if ! command -v jq >/dev/null 2>&1; then
    echo "jq is required to read summary.json." >&2
    exit 1
fi

repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
work=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-verify-dotnet-test.XXXXXXXX")
trap 'rm -rf "$work"' EXIT
mkdir "$work/bin"

cat > "$work/bin/dotnet" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_LOG"
printf '%s\n' "${DOTNET_PROCESSOR_COUNT-unset}" >> "$STUB_LOG.env"
case "$1" in
    --version) echo 10.0.402 ;;
    msbuild)
        for argument in "$@"; do
            case "$argument" in
                -getProperty:*) exit "${STUB_EVAL_STATUS:-0}" ;;
                -p:ManifestDirectory=*)
                    if [[ "${STUB_BLOCK_SUMMARY:-}" == 1 ]]; then mkdir "${argument#-p:ManifestDirectory=}/../summary.json"; fi
                    ;;
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
        previous=""
        for argument in "$@"; do
            if [[ "$previous" == --results-directory && -n "${STUB_REPORT:-}" ]]; then cp "$STUB_REPORT" "$argument/results.xml"; fi
            previous=$argument
        done
        ;;
    run)
        project=$3
        printf 'RUN %s %s\n' "$(basename "${project%.csproj}")" "$*" >> "$STUB_LOG"
        printf '%s\n' "${STUB_METHODS:-[]}"
        exit "${STUB_LIST_STATUS:-0}"
        ;;
    build)
        if [[ "$*" == *Parchwell.slnx* ]]; then exit "${STUB_BUILD_STATUS:-0}"; fi
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
    : > "$STUB_LOG.env"
    status=0
    env -u DOTNET_PROCESSOR_COUNT -u PARCHWELL_BUILD_MAX_CPU PATH="$work/bin:$PATH" TMPDIR="$work" ${environment[@]+"${environment[@]}"} \
        "$repository_root/scripts/verify-dotnet.sh" "$@" > "$work/$case_name.out" 2>&1 || status=$?
    output=$(<"$work/$case_name.out")
    log=$(<"$STUB_LOG")
    tested=$(grep -c '^TEST ' "$STUB_LOG" || true)
    results_dir=$(sed -n 's/^Verification logs and reports: //p' "$work/$case_name.out" | tail -n 1)
    summary="$results_dir/summary.json"
}

timestamp='[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z'

# check_summary <description> <jq filter>: the filter must print true for summary.json.
check_summary() {
    jq -e "$2" "$summary" >/dev/null 2>&1 || fail "summary.json: $1 (filter: $2)"
}

# Report fixtures: the writer's attribute order, each <test> on its own line.
{
    echo '<assemblies><assembly name="stub"><collection>'
    for index in $(seq 1 30); do printf '<test id="t%s" name="T%02d" result="Pass" time="0.%03d" />\n' "$index" "$index" "$index"; done
    echo '<test id="t31" name="T30b" result="Pass" time="0.030" />'
    echo '<test id="t32" name="Nine" result="Pass" time="9.5"><traits><trait name="Tier" value="Slow" /></traits></test>'
    echo '<test id="t33" name="Angle &lt;a&gt; &apos;b&apos; &amp;lt;" result="Pass" time="0.5" />'
    printf '%s\n' '<test id="t34" name="Q \&quot;quoted\&quot; &amp; back\\slash" result="Pass" time="12.5" />'
    echo '</collection></assembly></assemblies>'
} > "$work/slow.xml"
printf '<assemblies><assembly name="stub"><collection>\n<test id="t1" name="Only" result="Pass" time="0.25" />\n</collection></assembly></assemblies>\n' > "$work/one.xml"

run_case default --
[[ $status -eq 0 ]] || fail "default run exited $status"
[[ $tested -eq 6 ]] || fail "default run invoked $tested test projects, expected 6"
[[ "$log" != *filter-not-trait* ]] || fail "default run must not filter tests"
[[ "$log" != *--filter-class* && "$log" != *--filter-not-class* ]] || fail "default run must not pass a class filter"
[[ $(grep -c 'msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence' "$STUB_LOG") -eq 1 ]] || fail "tier all must publish the spike evidence once"
[[ "$output" == *"Verification passed. Per-project reports"* ]] || fail "default run must print the full-gate pass line"
awk -F '\t' -v pattern="^$timestamp\$" 'NF != 4 || $3 !~ pattern || $4 !~ pattern { bad = 1 } END { exit bad }' "$results_dir/stage-timing.tsv" \
    || fail "every stage-timing.tsv line must have 4 tab fields with UTC start and end"
grep -Eq "^Locked restore: passed \\([0-9]+ s, $timestamp to $timestamp\\)\$" "$work/default.out" || fail "stage lines must carry whole seconds and UTC start and end"
grep -qxF "Stage timing (seconds, UTC start and end), also in $results_dir/stage-timing.tsv:" "$work/default.out" || fail "timing table heading"
check_summary "stage labels and exit codes" '.stages[0].label == "Locked restore" and all(.stages[]; .exit_code == 0)'
[[ "$(jq '.stages | length' "$summary")" -eq "$(wc -l < "$results_dir/stage-timing.tsv")" ]] || fail "stages must match the tsv lines"
check_summary "outcome and counts" '.outcome == "full_gate_passed" and .exit_code == 0 and .failed_stages == [] and .slowest_tests == {} and .schema_version == 1'
expected_dirty=false
[[ -z "$(git -C "$repository_root" status --porcelain)" ]] || expected_dirty=true
jq -e --arg sha "$(git -C "$repository_root" rev-parse HEAD)" --argjson dirty "$expected_dirty" '.source_sha == $sha and .source_dirty == $dirty' "$summary" >/dev/null || fail "source_sha and source_dirty"
expected_memory=$(awk '/^MemTotal:/ { print $2; exit }' /proc/meminfo 2>/dev/null || true)
jq -e --argjson cpus "$(nproc)" --argjson memory "${expected_memory:-null}" '.nproc == $cpus and .mem_total_kib == $memory' "$summary" >/dev/null || fail "nproc and mem_total_kib"
check_summary "tier, projects, sdk and environment values" '.tier == "all" and .projects == null and .sdk_version == "10.0.402" and .dotnet_processor_count == null and .parchwell_build_max_cpu == null'
jq -e --arg pattern "^$timestamp\$" '(.started_utc | test($pattern)) and (.ended_utc | test($pattern))' "$summary" >/dev/null || fail "started_utc and ended_utc"
[[ "${output##*$'\n'}" == "Verification logs and reports: "* ]] || fail "the exit line must stay the last line"

# Shards (issue 452). Fixture shard files use real tabs.
verifier=Parchwell.Verifier.Tests
verifier_a_classes=""
for class_name in PredicateBoundary Boundary TimeoutPrecedence TerminalOutcome BoundedRead; do
    verifier_a_classes+="$verifier.Component.B10AbruptVerify${class_name}Tests "
done
verifier_a_expected="-- --filter-class ${verifier_a_classes}--report-xunit-xml"
verifier_all_classes=$(grep -v '^#' "$repository_root/scripts/dotnet-test-shards.tsv" | awk -F '\t' -v p="$verifier" '$1 == p { printf "%s ", $3 }')

run_case shard_a -- --project $verifier --shard b10-verify-a
[[ $status -eq 0 && $tested -eq 1 ]] || fail "S1: shard run exited $status and ran $tested test commands"
[[ "$(grep '^TEST ' "$STUB_LOG")" == *"$verifier_a_expected"* ]] || fail "S1: TEST line must carry the shard's classes in file order"
[[ "$(grep '^TEST ' "$STUB_LOG")" != *--filter-not-class* ]] || fail "S1: a named shard must not pass --filter-not-class"
check_summary "S1 shard summary" '.shard == "b10-verify-a" and .projects == ["Parchwell.Verifier.Tests"] and .outcome == "partial_passed"'
[[ "$output" == *"Partial verification passed"* ]] || fail "S14: a shard run must say it is not the full gate"

run_case shard_rest -- --project $verifier --shard rest
[[ $status -eq 0 && $tested -eq 1 ]] || fail "S2: rest run exited $status"
[[ "$(grep '^TEST ' "$STUB_LOG")" == *"-- --filter-not-class ${verifier_all_classes}--report-xunit-xml"* ]] || fail "S2: rest must exclude every listed class in file order"
[[ "$(grep '^TEST ' "$STUB_LOG")" != *"--filter-class "* ]] || fail "S2: rest must not pass --filter-class"
[[ "$(grep -o 'Parchwell.Verifier.Tests.Component' <<<"$verifier_all_classes" | wc -l)" -eq 9 ]] || fail "S2: the Verifier shard file must list nine classes"

run_case shard_equals -- --project=Parchwell.LoadGen.Tests --shard=b10-end-to-end
[[ $status -eq 0 && $tested -eq 1 ]] || fail "S3: equals form exited $status"
[[ "$(grep '^TEST ' "$STUB_LOG")" == *"-- --filter-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptEndToEndTests --report-xunit-xml"* ]] || fail "S3: end-to-end shard filter"

run_case shard_fast -- --tier fast --project Parchwell.LoadGen.Tests --shard rest
[[ "$(grep '^TEST ' "$STUB_LOG")" == *"-- --filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2 --filter-not-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptEndToEndTests --report-xunit-xml"* ]] || fail "S4: tier arguments must come before the class filter"

run_case shard_sandbox -- --tier sandbox --project Parchwell.LoadGen.Tests --shard rest
[[ $status -eq 0 ]] || fail "S19: sandbox shard run exited $status"
[[ "$(grep '^TEST ' "$STUB_LOG")" == *"-- --filter-not-trait Tier=CGroupV2 --filter-not-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptEndToEndTests --report-xunit-xml"* ]] || fail "S19: sandbox shard filter"
check_summary "S19 sandbox shard summary" '.tier == "sandbox" and .shard == "rest"'

# reject_shard <case> <expected message> <env assignments...> -- <arguments>: exit 2, no dotnet call, no results directory.
reject_shard() {
    local name=$1 message=$2
    shift 2
    mkdir "$work/tmp-$name"
    run_case "$name" TMPDIR="$work/tmp-$name" "$@"
    [[ $status -eq 2 ]] || fail "$name: exited $status, expected 2"
    [[ ! -s "$STUB_LOG" ]] || fail "$name: must not call dotnet"
    [[ -z "$(ls -A "$work/tmp-$name")" ]] || fail "$name: must not create a results directory"
    [[ "$output" == *"$message"* ]] || fail "$name: expected '$message', got: $output"
}
reject_shard shard_no_project "--shard needs exactly one --project." -- --shard rest
reject_shard shard_two_projects "--shard needs exactly one --project." -- --project $verifier --project Parchwell.LoadGen.Tests --shard rest
reject_shard shard_twice "--shard may be given once." -- --project $verifier --shard rest --shard rest
reject_shard shard_unknown "Unknown --shard 'nope' for $verifier. Shards: b10-verify-a b10-verify-b rest" -- --project $verifier --shard nope
reject_shard shard_unsharded "Parchwell.Scenarios.Tests has no shards in $repository_root/scripts/dotnet-test-shards.tsv." -- --project Parchwell.Scenarios.Tests --shard rest
reject_shard shard_no_value "Usage: ./scripts/verify-dotnet.sh" -- --project $verifier --shard

shards_fixture="$work/shards-fixture.tsv"
reject_shard shard_file_missing "Cannot read shard file $work/no-such-shards.tsv." PARCHWELL_TEST_SHARDS_FILE="$work/no-such-shards.tsv" -- --project $verifier --shard rest
# Each fixture is written line by line with printf; the first listed line is line 1 unless a comment comes first.
malformed_case() {
    local label=$1 expected_line=$2
    shift 2
    printf '%b' "$@" > "$shards_fixture"
    reject_shard "shard_bad_$label" "Malformed shard file line $expected_line: $shards_fixture." PARCHWELL_TEST_SHARDS_FILE="$shards_fixture" -- --project $verifier --shard rest
}
malformed_case two_fields 1 "$verifier\\ts1\\n"
malformed_case shard_rest 1 "$verifier\\trest\\t$verifier.A\\n"
malformed_case shard_upper 1 "$verifier\\tBad\\t$verifier.A\\n"
malformed_case class_prefix 1 "$verifier\\ts1\\tOther.X\\n"
malformed_case duplicate 3 "$verifier\\ts1\\t$verifier.A\\n# note\\n$verifier\\ts2\\t$verifier.A\\n"
malformed_case four_fields 1 "$verifier\\ts1\\t$verifier.A\\tx\\n"
malformed_case project_pattern 1 "Verifier.Tests\\ts1\\tVerifier.Tests.A\\n"
malformed_case class_space 1 "$verifier\\ts1\\t$verifier.A B\\n"

# S17: CRLF line ends parse like LF.
sed 's/$/\r/' "$repository_root/scripts/dotnet-test-shards.tsv" > "$shards_fixture"
run_case shard_crlf PARCHWELL_TEST_SHARDS_FILE="$shards_fixture" -- --project $verifier --shard b10-verify-a
[[ $status -eq 0 && "$(grep '^TEST ' "$STUB_LOG")" == *"$verifier_a_expected"* ]] || fail "S17: CRLF shard file must give S1's filter (status $status)"

# S18: a file with only ignored lines shards nothing.
printf '# none\n' > "$shards_fixture"
reject_shard shard_file_empty "$verifier has no shards in $shards_fixture." PARCHWELL_TEST_SHARDS_FILE="$shards_fixture" -- --project $verifier --shard rest

# Test list stage.
run_case list STUB_METHODS='["A.B.C"]' --
[[ $status -eq 0 ]] || fail "S10: list run exited $status"
[[ $(grep -c '^RUN ' "$STUB_LOG") -eq 6 ]] || fail "S10: expected six RUN lines"
[[ $(grep '^RUN ' "$STUB_LOG" | grep -c -- '--configuration Release --no-build --no-launch-profile -- -list methods/json -noColor') -eq 6 ]] || fail "S10: every RUN line must carry the list arguments"
for project in Parchwell.ArchitectureTests Parchwell.Enrichment.Tests Parchwell.Kafka.Tests Parchwell.LoadGen.Tests Parchwell.Scenarios.Tests Parchwell.Verifier.Tests; do
    printf '["A.B.C"]\n' | cmp -s - "$results_dir/$project/methods.json" || fail "S10: $project methods.json must equal the stub's output byte for byte"
    jq -e --arg p "$project" '[.stages[].label] as $l | ($l | index($p + " report")) + 1 == ($l | index($p + " test list")) and ([.stages[] | select(.label == ($p + " test list"))][0].exit_code == 0)' "$summary" >/dev/null \
        || fail "S10: $project test list stage must follow its report stage and pass"
done
check_summary "S10 shard null" '.shard == null'

run_case list_fail STUB_LIST_STATUS=3 --
[[ $status -eq 3 ]] || fail "S11: failing test list exited $status, expected 3"
check_summary "S11 failed stages" '[.failed_stages[] | .exit_code] == [3,3,3,3,3,3] and ([.failed_stages[].label] == ["Parchwell.ArchitectureTests","Parchwell.Enrichment.Tests","Parchwell.Kafka.Tests","Parchwell.LoadGen.Tests","Parchwell.Scenarios.Tests","Parchwell.Verifier.Tests"])'
[[ "$log" == *"build"*"Forbidden.cs"* ]] || fail "S11: warning control must still run"

run_case list_skipped STUB_FAIL=Parchwell.Kafka.Tests --
[[ $(grep -c '^RUN ' "$STUB_LOG") -eq 5 && "$(grep '^RUN ' "$STUB_LOG")" != *Parchwell.Kafka.Tests* ]] || fail "S12: a failed tests stage must skip the test list stage"

run_case fast -- --tier fast
[[ $status -eq 0 ]] || fail "fast tier exited $status"
[[ $tested -eq 6 ]] || fail "fast tier invoked $tested test projects, expected 6"
[[ $(grep -c '^TEST .*--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2' "$STUB_LOG") -eq 6 ]] \
    || fail "every fast-tier test run must exclude Tier=Docker, Tier=Slow and Tier=CGroupV2"
[[ "$output" == *"Partial verification passed"* && "$output" != *"Verification passed. Per-project reports"* ]] \
    || fail "fast tier must say it is not the full gate"
check_summary "fast tier" '.outcome == "partial_passed" and .tier == "fast" and .projects == null'
[[ $(grep -c 'msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence' "$STUB_LOG") -eq 0 ]] || fail "fast tier must not publish the spike evidence"

run_case sandbox -- --tier sandbox
[[ $status -eq 0 ]] || fail "sandbox tier exited $status"
[[ $tested -eq 6 ]] || fail "sandbox tier invoked $tested test projects, expected 6"
[[ $(grep '^TEST ' "$STUB_LOG" | grep -c -- '--filter-not-trait Tier=CGroupV2') -eq 6 ]] \
    || fail "every sandbox-tier test run must carry --filter-not-trait Tier=CGroupV2"
[[ "$log" != *Tier=Docker* && "$log" != *Tier=Slow* ]] || fail "sandbox tier must not exclude Tier=Docker or Tier=Slow"
[[ "$output" == *"Partial verification passed (tier sandbox, projects: all). This is not the full gate"* && "$output" != *"Verification passed. Per-project reports"* ]] \
    || fail "sandbox tier must say it is not the full gate"
check_summary "sandbox tier" '.outcome == "partial_passed" and .tier == "sandbox" and .projects == null'
[[ $(grep -c 'msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence' "$STUB_LOG") -eq 1 ]] || fail "sandbox tier must publish the spike evidence once"

run_case sandbox_equals -- --tier=sandbox
[[ $status -eq 0 && $tested -eq 6 ]] || fail "--tier=sandbox exited $status and invoked $tested projects"
[[ $(grep '^TEST ' "$STUB_LOG" | grep -c -- '--filter-not-trait Tier=CGroupV2') -eq 6 ]] || fail "--tier=sandbox must filter every test run"
check_summary "sandbox tier with equals sign" '.tier == "sandbox"'

run_case sandbox_project -- --tier sandbox --project Parchwell.Scenarios.Tests
[[ $status -eq 0 && $tested -eq 1 ]] || fail "sandbox with --project exited $status and invoked $tested projects"
[[ $(grep '^TEST ' "$STUB_LOG") == *"Parchwell.Scenarios.Tests"*"--filter-not-trait Tier=CGroupV2"* ]] || fail "sandbox with --project must run Parchwell.Scenarios.Tests with the filter"
check_summary "sandbox tier with --project" '.tier == "sandbox" and .projects == ["Parchwell.Scenarios.Tests"]'

run_case fail_late STUB_FAIL="Parchwell.Kafka.Tests Parchwell.Verifier.Tests" --
[[ $status -eq 2 ]] || fail "fail-late run exited $status, expected the first failure's status 2"
[[ $tested -eq 6 ]] || fail "fail-late run invoked $tested test projects, expected all 6 despite two failures"
[[ "$output" == *"Verification failed in 2 stage(s)"* && "$output" == *"Parchwell.Kafka.Tests (exit 2)"* && "$output" == *"Parchwell.Verifier.Tests (exit 2)"* ]] \
    || fail "fail-late run must list both failed projects"
[[ "$log" == *"build"*"Forbidden.cs"* ]] || fail "fail-late run must still reach the warning control"
check_summary "failed stages" '.failed_stages == [{"label":"Parchwell.Kafka.Tests","exit_code":2},{"label":"Parchwell.Verifier.Tests","exit_code":2}] and .outcome == "failed" and .exit_code == 2'
check_summary "failed test stage" '[.stages[] | select(.label == "Parchwell.Kafka.Tests tests")][0].exit_code == 2'

run_case select -- --project Parchwell.Scenarios.Tests
[[ $status -eq 0 && $tested -eq 1 && "$log" == *"TEST Parchwell.Scenarios.Tests"* ]] || fail "--project must run only the named project (status $status, ran $tested)"
[[ "$output" == *"Partial verification passed"* ]] || fail "--project must say it is not the full gate"
check_summary "selected project" '.outcome == "partial_passed" and .projects == ["Parchwell.Scenarios.Tests"]'

mkdir "$work/tmp-unknown_project"
run_case unknown_project TMPDIR="$work/tmp-unknown_project" -- --project Parchwell.Nope
[[ $status -eq 2 && $tested -eq 0 && ! -s "$STUB_LOG" ]] || fail "unknown --project must exit 2 before any dotnet call (status $status, ran $tested)"
[[ -z "$(ls -A "$work/tmp-unknown_project")" ]] || fail "unknown --project must not create a results directory"
mkdir "$work/tmp-unknown_tier"
run_case unknown_tier TMPDIR="$work/tmp-unknown_tier" -- --tier slow
[[ $status -eq 2 && ! -s "$STUB_LOG" ]] || fail "unknown --tier must exit 2 before any dotnet call (status $status)"
[[ -z "$(ls -A "$work/tmp-unknown_tier")" ]] || fail "unknown --tier must not create a results directory"
[[ "$output" == "Unknown tier: slow (expected all, fast or sandbox)." ]] || fail "unknown --tier message: $output"
mkdir "$work/tmp-bad_cpu"
run_case bad_cpu TMPDIR="$work/tmp-bad_cpu" PARCHWELL_BUILD_MAX_CPU=0 --
[[ $status -eq 2 && ! -s "$STUB_LOG" ]] || fail "invalid PARCHWELL_BUILD_MAX_CPU must exit 2 before any dotnet call (status $status)"
[[ -z "$(ls -A "$work/tmp-bad_cpu")" ]] || fail "invalid PARCHWELL_BUILD_MAX_CPU must not create a results directory"
mkdir "$work/tmp-help"
run_case help TMPDIR="$work/tmp-help" -- --help
[[ $status -eq 0 && "$output" == *"Each run writes summary.json and stage-timing.tsv to its results directory."* ]] || fail "--help must exit 0 and name summary.json (status $status)"
[[ -z "$(ls -A "$work/tmp-help")" ]] || fail "--help must not create a results directory"
grep -qF '[--tier fast|sandbox|all]' "$work/help.out" || fail "--help must show the sandbox tier in the synopsis"
grep -Eq '^ *--tier sandbox .*Tier=CGroupV2' "$work/help.out" || fail "--help must describe --tier sandbox"
grep -qxF 'A run with --tier fast, --tier sandbox or --project prints that it is not the full gate.' "$work/help.out" || fail "--help must name the sandbox tier as a partial run"
grep -qF -- '--shard' "$work/help.out" || fail "--help must name --shard"
grep -qF 'PARCHWELL_TEST_SHARDS_FILE' "$work/help.out" || fail "--help must name PARCHWELL_TEST_SHARDS_FILE"

run_case cpu PARCHWELL_BUILD_MAX_CPU=3 DOTNET_PROCESSOR_COUNT=2 --
[[ $status -eq 0 ]] || fail "cpu-capped run exited $status"
grep -q '^restore Parchwell.slnx .*-maxcpucount:3' "$STUB_LOG" || fail "restore must receive -maxcpucount:3"
grep -q '^build Parchwell.slnx .*-maxcpucount:3' "$STUB_LOG" || fail "build must receive -maxcpucount:3"
if grep '^TEST ' "$STUB_LOG" | grep -q maxcpucount; then fail "test runs must not receive -maxcpucount"; fi
check_summary "environment values" '.parchwell_build_max_cpu == "3" and .dotnet_processor_count == "2"'
[[ "$(sort -u "$STUB_LOG.env")" == 2 ]] || fail "every dotnet call must see DOTNET_PROCESSOR_COUNT as the caller set it"
if grep -q DOTNET_PROCESSOR_COUNT "$STUB_LOG"; then fail "the script must not pass DOTNET_PROCESSOR_COUNT to dotnet"; fi

run_case cpu_text DOTNET_PROCESSOR_COUNT=$'a"b\\c\td' --
jq -e . "$summary" >/dev/null || fail "summary.json must stay valid JSON for special characters"
[[ "$(jq -r .dotnet_processor_count "$summary")" == $'a"b\\c\td' ]] || fail "dotnet_processor_count must round-trip special characters"

run_case warning_accepted STUB_ACCEPT_WARNING=1 --
[[ $status -ne 0 && $tested -eq 6 ]] || fail "an accepted intentional warning must fail the run after all tests (status $status, ran $tested)"
[[ "$output" == *"Warning control (exit"* ]] || fail "the failed warning control must be listed"
check_summary "warning control" '.failed_stages[-1] == {"label":"Warning control","exit_code":1} and .outcome == "failed"'

run_case build_fail STUB_BUILD_STATUS=4 --
[[ $status -eq 4 && $tested -eq 0 ]] || fail "a failed build must stop the run with its status (status $status, ran $tested)"
check_summary "build failure" '.failed_stages == [{"label":"Solution build","exit_code":4}] and .stages[-1].label == "Solution build" and .stages[-1].exit_code == 4 and .outcome == "failed" and .exit_code == 4'
grep -Eq "^Solution build: failed \\(exit 4, [0-9]+ s, $timestamp to $timestamp\\)\$" "$work/build_fail.out" || fail "failed stage line must carry UTC start and end"

run_case eval_fail STUB_EVAL_STATUS=5 --
[[ $status -eq 5 && $tested -eq 0 ]] || fail "a failed project evaluation must stop the run with its status (status $status, ran $tested)"
check_summary "evaluation failure" '.failed_stages == [{"label":"Project evaluation","exit_code":5}] and .exit_code == 5 and .outcome == "failed"'

run_case slowest STUB_REPORT="$work/slow.xml" --
[[ $status -eq 0 ]] || fail "slowest run exited $status"
[[ "$(jq '.slowest_tests | length' "$summary")" -eq 6 ]] || fail "every project report must appear in slowest_tests"
for project in $(jq -r '.slowest_tests | keys[]' "$summary"); do
    [[ "$(jq --arg p "$project" '.slowest_tests[$p] | length' "$summary")" -eq 30 ]] || fail "$project must list 30 slowest tests"
    [[ "$(jq -c --arg p "$project" '.slowest_tests[$p][0] | {name, time_s}' "$summary")" == '{"name":"Q \\\"quoted\\\" & back\\\\slash","time_s":"12.5"}' ]] || fail "$project entry 0 must be the escaped Q name"
    jq -e --arg p "$project" '.slowest_tests[$p] as $t | $t[1].name == "Nine" and $t[1].time_s == "9.5" and $t[2].name == "Angle <a> '"'"'b'"'"' &lt;" and $t[3].name == "T30" and $t[4].name == "T30b" and $t[29].name == "T05" and ([$t[].name] | index("T04") == null and index("T01") == null)' "$summary" >/dev/null \
        || fail "$project slowest order or decoding"
done

run_case slowest_one STUB_REPORT="$work/one.xml" -- --project Parchwell.Scenarios.Tests
check_summary "single report" '.slowest_tests == {"Parchwell.Scenarios.Tests":[{"name":"Only","time_s":"0.25"}]}'

run_case no_git GIT_DIR="$work/nogit" --
[[ $status -eq 0 && "$output" == *"Verification passed. Per-project reports"* ]] || fail "a missing git directory must not change the run (status $status)"
check_summary "no git" '.source_sha == null and .source_dirty == null'

# A failure right after the results directory exists must still print the exit line and write summary.json.
mkdir "$work/failbin"
cat > "$work/failbin/mkdir" <<'FAILMKDIR'
#!/usr/bin/env bash
for argument in "$@"; do
    if [[ "$argument" == */manifests ]]; then echo "mkdir: simulated failure" >&2; exit 1; fi
done
exec /bin/mkdir "$@"
FAILMKDIR
chmod +x "$work/failbin/mkdir"
run_case early_failure PATH="$work/failbin:$work/bin:$PATH" --
[[ $status -eq 1 && "${output##*$'\n'}" == "Verification logs and reports: "* ]] || fail "an early failure must keep its status and print the exit line last (status $status)"
check_summary "early failure" '.outcome == "failed" and .exit_code == 1 and .stages == [] and .failed_stages == []'

run_case summary_unwritable STUB_BLOCK_SUMMARY=1 --
[[ $status -eq 0 && "$output" == *"Verification passed. Per-project reports identify the tests executed."* ]] || fail "an unwritable summary must not change the exit status (status $status)"
mapfile -t summary_unwritable_lines < "$work/summary_unwritable.out"
last_index=$((${#summary_unwritable_lines[@]} - 1))
[[ "${summary_unwritable_lines[last_index - 1]}" == "summary.json could not be written." && "${summary_unwritable_lines[last_index]}" == "Verification logs and reports: "* ]] \
    || fail "an unwritable summary must be reported just before the exit line"
[[ -d "$results_dir/summary.json" ]] || fail "the summary_unwritable fixture must block summary.json with a directory"

if [[ $failures -gt 0 ]]; then
    echo "$failures check(s) failed. Outputs: $work" >&2
    trap - EXIT
    exit 1
fi
echo "verify-dotnet.sh checks passed."
