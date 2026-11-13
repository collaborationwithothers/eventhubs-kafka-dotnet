#!/usr/bin/env bash
set -euo pipefail

repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"

usage() {
    cat <<'USAGE'
Usage: ./scripts/verify-dotnet.sh [--tier fast|all] [--project <test project name>]...

With no options the script runs the full gate: every test project, every test.
  --tier all    Run every test (default).
  --tier fast   Leave out tests marked with the xUnit trait Tier=Docker, Tier=Slow or Tier=Host.
  --project     Run only the named test project, for example Parchwell.Verifier.Tests. Repeatable.
Environment:
  PARCHWELL_BUILD_MAX_CPU  Positive integer. Adds -maxcpucount:<n> to restore, build and project
                           evaluation only. Tests keep the machine's real processor count.
A run with --tier fast or --project prints that it is not the full gate.
Each run writes summary.json and stage-timing.tsv to its results directory.
USAGE
}

tier=all
selected_projects=()
while (($# > 0)); do
    case "$1" in
        --tier)
            if (($# < 2)); then usage >&2; exit 2; fi
            tier=$2
            shift 2
            ;;
        --tier=*)
            tier=${1#--tier=}
            shift
            ;;
        --project)
            if (($# < 2)); then usage >&2; exit 2; fi
            selected_projects+=("$2")
            shift 2
            ;;
        --project=*)
            selected_projects+=("${1#--project=}")
            shift
            ;;
        -h | --help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done
case "$tier" in
    all) tier_args=() ;;
    fast) tier_args=(--filter-not-trait "Tier=Docker" "Tier=Slow" "Tier=Host") ;;
    *)
        echo "Unknown tier: $tier (expected fast or all)." >&2
        exit 2
        ;;
esac
build_cpu_args=()
if [[ -n "${PARCHWELL_BUILD_MAX_CPU:-}" ]]; then
    if [[ ! "$PARCHWELL_BUILD_MAX_CPU" =~ ^[1-9][0-9]*$ ]]; then
        echo "PARCHWELL_BUILD_MAX_CPU must be a positive integer; got '$PARCHWELL_BUILD_MAX_CPU'." >&2
        exit 2
    fi
    build_cpu_args=("-maxcpucount:$PARCHWELL_BUILD_MAX_CPU")
fi

# Each --project name must be a test project of the solution. Checked before any build.
if ((${#selected_projects[@]} > 0)); then
    shopt -s globstar nullglob
    known_test_projects=()
    for project in tests/**/*.csproj; do known_test_projects+=("$(basename "${project%.csproj}")"); done
    for requested in "${selected_projects[@]}"; do
        found=false
        for candidate in "${known_test_projects[@]}"; do
            if [[ "$candidate" == "$requested" ]]; then found=true; fi
        done
        if [[ "$found" != true ]]; then
            echo "Unknown --project '$requested'. Test projects: ${known_test_projects[*]}" >&2
            exit 2
        fi
    done
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "Install .NET SDK 10.0.402 separately, then rerun ./scripts/verify-dotnet.sh." >&2
    exit 1
fi
if sdk_version=$(dotnet --version); then
    if [[ "$sdk_version" != "10.0.402" ]]; then
        echo "Expected .NET SDK 10.0.402; selected $sdk_version." >&2
        exit 1
    fi
else
    exit "$?"
fi

verification_results=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-dotnet-verification.XXXXXXXX")
started_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)
mkdir -p "$verification_results/manifests"
export PARCHWELL_REPOSITORY_ROOT="$repository_root"
export PARCHWELL_PROJECT_MANIFEST_DIRECTORY="$verification_results/manifests"
stage_timing="$verification_results/stage-timing.tsv"
failed_stages=()
failed_stage_labels=()
failed_stage_codes=()
stage_labels=()
stage_elapsed=()
stage_starts=()
stage_ends=()
stage_codes=()
stopping_label=""
outcome=failed
first_failure_status=0

# Machine and source facts for summary.json. Each is empty when it cannot be read.
source_sha=""
source_dirty=""
if sha=$(git rev-parse HEAD 2>/dev/null) && [[ -n "$sha" ]]; then
    source_sha=$sha
    if porcelain=$(git status --porcelain 2>/dev/null); then
        if [[ -n "$porcelain" ]]; then source_dirty=true; else source_dirty=false; fi
    fi
fi
nproc_value=""
if count=$(nproc 2>/dev/null) && [[ "$count" =~ ^[0-9]+$ ]]; then nproc_value=$count; fi
mem_total_kib=""
if memory=$(awk '/^MemTotal:/ { print $2; exit }' /proc/meminfo 2>/dev/null) && [[ "$memory" =~ ^[0-9]+$ ]]; then
    mem_total_kib=$memory
fi

# Sets REPLY to the JSON string literal for $1: backslash, double quote and bytes below 0x20 are escaped.
json_string() {
    local LC_ALL=C
    local text=$1 escaped="" index character hex
    text=${text//\\/\\\\}
    text=${text//\"/\\\"}
    if [[ "$text" == *[$'\001'-$'\037']* ]]; then
        for ((index = 0; index < ${#text}; index++)); do
            character=${text:index:1}
            if [[ "$character" == [$'\001'-$'\037'] ]]; then
                printf -v hex '\\u%04x' "'$character"
                escaped+=$hex
            else
                escaped+=$character
            fi
        done
        text=$escaped
    fi
    REPLY="\"$text\""
}

# Prints the JSON object for slowest_tests: the 30 slowest <test> elements of each results.xml.
slowest_tests_json() {
    local report project_name test_time test_name separator="" object_separator="" rows=""
    printf '{'
    for report in "$verification_results"/*/results.xml; do
        [[ -f "$report" ]] || continue
        project_name=$(basename "$(dirname "$report")")
        rows=$(
            awk 'BEGIN { RS = "<" }
                /^test[ \t\r\n]/ {
                    if (match($0, /[ \t\r\n]name="[^"]*"/)) { name = substr($0, RSTART + 7, RLENGTH - 8) } else { next }
                    if (match($0, /[ \t\r\n]time="[^"]*"/)) { time = substr($0, RSTART + 7, RLENGTH - 8) } else { next }
                    printf "%s\t%s\n", time, name
                }' "$report" 2>/dev/null \
                | while IFS=$'\t' read -r test_time test_name; do
                    test_name=${test_name//"&lt;"/"<"}
                    test_name=${test_name//"&gt;"/">"}
                    test_name=${test_name//"&quot;"/'"'}
                    test_name=${test_name//"&apos;"/"'"}
                    test_name=${test_name//"&amp;"/"&"}
                    printf '%s\t%s\n' "$test_time" "$test_name"
                done \
                | LC_ALL=C sort -t $'\t' -k1,1gr -k2,2 | head -n 30
        ) || true
        json_string "$project_name"
        printf '%s%s:[' "$object_separator" "$REPLY"
        object_separator=","
        separator=""
        while IFS=$'\t' read -r test_time test_name; do
            [[ -n "$test_time" ]] || continue
            json_string "$test_name"
            printf '%s{"name":%s,' "$separator" "$REPLY"
            json_string "$test_time"
            printf '"time_s":%s}' "$REPLY"
            separator=","
        done <<<"$rows"
        printf ']'
    done
    printf '}'
}

# Prints summary.json. $1 is the script's exit status.
summary_json() {
    local exit_status=$1 index separator="" value
    printf '{"schema_version":1,'
    printf '"started_utc":"%s","ended_utc":"%s",' "$started_utc" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    if [[ -n "$source_sha" ]]; then json_string "$source_sha"; printf '"source_sha":%s,' "$REPLY"; else printf '"source_sha":null,'; fi
    printf '"source_dirty":%s,' "${source_dirty:-null}"
    json_string "$sdk_version"; printf '"sdk_version":%s,' "$REPLY"
    json_string "$tier"; printf '"tier":%s,' "$REPLY"
    if ((${#selected_projects[@]} == 0)); then
        printf '"projects":null,'
    else
        printf '"projects":['
        for value in "${selected_projects[@]}"; do
            json_string "$value"; printf '%s%s' "$separator" "$REPLY"; separator=","
        done
        printf '],'
    fi
    printf '"nproc":%s,"mem_total_kib":%s,' "${nproc_value:-null}" "${mem_total_kib:-null}"
    if [[ -n "${DOTNET_PROCESSOR_COUNT+set}" ]]; then
        json_string "$DOTNET_PROCESSOR_COUNT"; printf '"dotnet_processor_count":%s,' "$REPLY"
    else
        printf '"dotnet_processor_count":null,'
    fi
    if [[ -n "${PARCHWELL_BUILD_MAX_CPU:-}" ]]; then
        json_string "$PARCHWELL_BUILD_MAX_CPU"; printf '"parchwell_build_max_cpu":%s,' "$REPLY"
    else
        printf '"parchwell_build_max_cpu":null,'
    fi
    printf '"stages":['
    separator=""
    for ((index = 0; index < ${#stage_labels[@]}; index++)); do
        json_string "${stage_labels[index]}"
        printf '%s{"label":%s,"elapsed_s":%s,"start_utc":"%s","end_utc":"%s","exit_code":%s}' \
            "$separator" "$REPLY" "${stage_elapsed[index]}" "${stage_starts[index]}" "${stage_ends[index]}" "${stage_codes[index]}"
        separator=","
    done
    printf '],"failed_stages":['
    separator=""
    if ((${#failed_stage_labels[@]} > 0)); then
        for ((index = 0; index < ${#failed_stage_labels[@]}; index++)); do
            json_string "${failed_stage_labels[index]}"
            printf '%s{"label":%s,"exit_code":%s}' "$separator" "$REPLY" "${failed_stage_codes[index]}"
            separator=","
        done
    elif [[ "$exit_status" -ne 0 && -n "$stopping_label" ]]; then
        json_string "$stopping_label"
        printf '{"label":%s,"exit_code":%s}' "$REPLY" "$exit_status"
    fi
    printf '],"exit_code":%s,"outcome":"%s","slowest_tests":' "$exit_status" "$outcome"
    slowest_tests_json
    printf '}\n'
}

# Runs on every exit after the results directory exists. Writing the summary never changes the exit status.
on_exit() {
    local exit_status=$?
    local summary
    if summary=$(summary_json "$exit_status" 2>/dev/null) && { printf '%s\n' "$summary" >"$verification_results/summary.json"; } 2>/dev/null; then
        :
    else
        echo "summary.json could not be written." >&2
    fi
    echo "Verification logs and reports: $verification_results"
}
trap on_exit EXIT

# Prints "<label>: passed (<n> s, <start> to <end>)" or the failure with the log tail, and records
# the duration and the UTC start and end.
run_logged() {
    local label=$1 logfile=$2
    shift 2
    local started=$SECONDS status=0 start_utc end_utc
    start_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    "$@" >"$logfile" 2>&1 || status=$?
    end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    local elapsed=$((SECONDS - started))
    printf '%s\t%s\t%s\t%s\n' "$label" "$elapsed" "$start_utc" "$end_utc" >>"$stage_timing"
    stage_labels+=("$label")
    stage_elapsed+=("$elapsed")
    stage_starts+=("$start_utc")
    stage_ends+=("$end_utc")
    stage_codes+=("$status")
    if [[ "$status" -eq 0 ]]; then
        echo "$label: passed ($elapsed s, $start_utc to $end_utc)"
    else
        stopping_label=$label
        echo "$label: failed (exit $status, $elapsed s, $start_utc to $end_utc)" >&2
        tail -n 80 "$logfile" >&2
        return "$status"
    fi
}

# Records a failed stage and lets the run continue to the remaining stages.
record_failure() {
    local label=$1 status=$2
    failed_stages+=("$label (exit $status)")
    failed_stage_labels+=("$label")
    failed_stage_codes+=("$status")
    if [[ "$first_failure_status" -eq 0 ]]; then first_failure_status=$status; fi
}

# Callers run this inside "|| record_failure", which disables errexit in the whole function body,
# so each command here that can fail must end in an explicit "|| return".
run_tests() {
    local project=$1 name=$2
    local results="$verification_results/$name"
    mkdir -p "$results" || return $?
    local test_status=0 evidence_status=0
    if [[ "$name" == "Parchwell.Kafka.Tests" ]]; then
        export PARCHWELL_SPIKE_EVIDENCE_DIRECTORY="$results/spike"
    fi
    if [[ "$name" == "Parchwell.LoadGen.Tests" ]]; then
        run_logged "Published LoadGen fixture" "$results/publish.log" dotnet publish \
            src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release \
            --no-build --no-restore --output "$results/publish" || return $?
        local -x PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST="$results/publish/Parchwell.LoadGen"
    fi
    run_logged "$name tests" "$results/test.log" dotnet test \
        --project "$project" --configuration Release --no-build \
        --minimum-expected-tests 1 --results-directory "$results" -- \
        ${tier_args[@]+"${tier_args[@]}"} \
        --report-xunit-xml --report-xunit-xml-filename results.xml || test_status=$?
    # The spike evidence comes from the Docker spike test, which the fast tier leaves out.
    if [[ "$name" == "Parchwell.Kafka.Tests" && "$tier" == all ]]; then
        dotnet msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence \
            "-p:EvidenceDirectory=$PARCHWELL_SPIKE_EVIDENCE_DIRECTORY" -nologo || evidence_status=$?
        unset PARCHWELL_SPIKE_EVIDENCE_DIRECTORY
    fi
    if [[ "$test_status" -ne 0 ]]; then
        cat "$results/test.log" >&2
        return "$test_status"
    fi
    if [[ "$evidence_status" -ne 0 ]]; then return "$evidence_status"; fi
    run_logged "$name report" "$results/report.log" dotnet msbuild scripts/dotnet-verification.proj \
        -t:ValidateReport "-p:Report=$results/results.xml" "-p:ExpectedAssembly=$name" -nologo || return $?
    cat "$results/report.log"
}

# True when no --project option was given or the named project was given.
is_selected() {
    local name=$1 candidate
    if ((${#selected_projects[@]} == 0)); then return 0; fi
    for candidate in "${selected_projects[@]}"; do
        if [[ "$candidate" == "$name" ]]; then return 0; fi
    done
    return 1
}

# Same rule as the original inline sequence, as a function so a failure can be recorded.
warning_control() {
    local warning_fixture="$verification_results/warning-control"
    mkdir -p "$warning_fixture" || return $?
    cp tests/verification-fixtures/warnings/WarningControl.csproj.fixture "$warning_fixture/WarningControl.csproj" || return $?
    cp tests/verification-fixtures/warnings/Allowed.cs.fixture "$warning_fixture/Allowed.cs" || return $?
    cp tests/verification-fixtures/warnings/Forbidden.cs.fixture "$warning_fixture/Forbidden.cs" || return $?
    run_logged "Warning control restore" "$warning_fixture/restore.log" dotnet restore "$warning_fixture/WarningControl.csproj" \
        ${build_cpu_args[@]+"${build_cpu_args[@]}"} \
        "-p:ParchwellRepositoryRoot=$repository_root" "-p:ControlSource=$warning_fixture/Allowed.cs" || return $?
    run_logged "Warning control valid counterpart" "$warning_fixture/allowed.log" dotnet build "$warning_fixture/WarningControl.csproj" \
        --configuration Release --no-restore ${build_cpu_args[@]+"${build_cpu_args[@]}"} \
        "-p:ParchwellRepositoryRoot=$repository_root" "-p:ControlSource=$warning_fixture/Allowed.cs" || return $?
    local status=0
    dotnet build "$warning_fixture/WarningControl.csproj" --configuration Release --no-restore \
        ${build_cpu_args[@]+"${build_cpu_args[@]}"} \
        "-p:ParchwellRepositoryRoot=$repository_root" "-p:ControlSource=$warning_fixture/Forbidden.cs" >"$warning_fixture/forbidden.log" 2>&1 || status=$?
    if [[ "$status" -eq 0 ]]; then
        echo "Warning control failed: the intentional compiler warning was accepted." >&2
        return 1
    fi
    local expected_diagnostic="error CS1030: #warning: 'PARCHWELL_WARNING_CONTROL'"
    if [[ "$status" -ne 1 || "$(<"$warning_fixture/forbidden.log")" != *"$expected_diagnostic"* ]]; then
        echo "Warning control failed for an unrelated cause (exit $status)." >&2
        tail -n 80 "$warning_fixture/forbidden.log" >&2
        return "$status"
    fi
    local unexpected_diagnostic=false diagnostic
    while IFS= read -r diagnostic || [[ -n "$diagnostic" ]]; do
        if [[ "$diagnostic" =~ (^|[[:space:]])[Ee][Rr][Rr][Oo][Rr][[:space:]] && "$diagnostic" != *"$expected_diagnostic"* ]]; then
            unexpected_diagnostic=true
            break
        fi
    done < "$warning_fixture/forbidden.log"
    if [[ "$unexpected_diagnostic" == true ]]; then
        echo "Warning control included an unrelated error (exit $status)." >&2
        tail -n 80 "$warning_fixture/forbidden.log" >&2
        return "$status"
    fi
    echo "Warning-as-error control: valid counterpart built; intended CS1030 error rejected."
}

echo "Verifying with .NET SDK $sdk_version (Release), tier $tier."
# Restore, build, inventory and evaluation stay fail-fast: no later stage can run without them.
run_logged "Locked restore" "$verification_results/restore.log" dotnet restore Parchwell.slnx --locked-mode ${build_cpu_args[@]+"${build_cpu_args[@]}"}
run_logged "Solution build" "$verification_results/build.log" dotnet build Parchwell.slnx --configuration Release --no-restore ${build_cpu_args[@]+"${build_cpu_args[@]}"}
run_logged "Kafka XML inventory consistency" "$verification_results/kafka-documentation.log" \
    dotnet msbuild scripts/dotnet-verification.proj -t:ValidateKafkaDocumentation -nologo
cat "$verification_results/kafka-documentation.log"

shopt -s globstar nullglob
projects=(src/**/*.csproj tests/**/*.csproj)
for project in "${projects[@]}"; do
    manifest="$verification_results/manifests/$(basename "${project%.csproj}").json"
    if dotnet msbuild "$project" -p:Configuration=Release -t:AddImplicitDefineConstants,_DisableDiagnosticTracing \
        ${build_cpu_args[@]+"${build_cpu_args[@]}"} \
        -getProperty:MSBuildProjectFullPath,TargetFramework,Nullable,TreatWarningsAsErrors,AnalysisLevel,EnableNETAnalyzers,RunAnalyzersDuringBuild,RunAnalyzers,AssemblyName,RootNamespace,ProjectDir,DefineConstants,LangVersion,EnforceCodeStyleInBuild \
        -getItem:ProjectReference,Compile,CompilerVisibleProperty -nologo >"$manifest" 2>"$manifest.stderr"; then
        :
    else
        status=$?
        stopping_label="Project evaluation"
        cat "$manifest" "$manifest.stderr" >&2
        exit "$status"
    fi
done
echo "Effective Release project settings and references: collected."

run_logged "Unit scaffold inventory" "$verification_results/scaffolds.log" dotnet msbuild scripts/dotnet-verification.proj \
    -t:SelectTests "-p:ManifestDirectory=$verification_results/manifests" "-p:ProjectList=$verification_results/unit-projects.txt" -nologo
cat "$verification_results/scaffolds.log"

# From here on a failing stage is recorded and the run continues, so one run reports every failure.
if is_selected Parchwell.ArchitectureTests; then
    run_tests tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj Parchwell.ArchitectureTests \
        || record_failure Parchwell.ArchitectureTests $?
fi
while IFS= read -r project; do
    name=$(basename "${project%.csproj}")
    if is_selected "$name"; then
        run_tests "$project" "$name" || record_failure "$name" $?
    fi
done < "$verification_results/unit-projects.txt"
for requested in ${selected_projects[@]+"${selected_projects[@]}"}; do
    if [[ "$requested" != Parchwell.ArchitectureTests ]] && ! grep -qF "/$requested.csproj" "$verification_results/unit-projects.txt"; then
        echo "$requested is an empty scaffold; there is nothing to run."
    fi
done

warning_control || record_failure "Warning control" $?

echo "Stage timing (seconds, UTC start and end), also in $stage_timing:"
awk -F '\t' '{ printf "  %-48s %s  %s  %s\n", $1, $2, $3, $4 }' "$stage_timing"
if ((${#failed_stages[@]} > 0)); then
    echo "Verification failed in ${#failed_stages[@]} stage(s):" >&2
    printf '  %s\n' "${failed_stages[@]}" >&2
    exit "$first_failure_status"
fi
if [[ "$tier" == all && ${#selected_projects[@]} -eq 0 ]]; then
    outcome=full_gate_passed
    echo "Verification passed. Per-project reports identify the tests executed."
else
    outcome=partial_passed
    echo "Partial verification passed (tier $tier, projects: ${selected_projects[*]:-all}). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options."
fi
