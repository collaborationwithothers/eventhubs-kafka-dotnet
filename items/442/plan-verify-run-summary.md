# Plan: verification run summary and environment-limited mode

Issue: [#442](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442). Pull request short name: `verify-run-summary`. Revision 1.

This plan adds UTC start and end stamps to every stage of `scripts/verify-dotnet.sh`, a `summary.json` run summary, and environment probes that turn a run without Docker or without the cgroup v2 file into an explicitly labelled environment-limited run. The default invocation keeps every test when both probes succeed. Two points stay open for Hari: the exit status of an environment-limited run, and whether the hosted runner passes the cgroup probe.

## 1. Goal and authority

Goal: a verification run records when each stage ran and on what machine, and a sandbox without Docker or without `memory.events.local` can run every other test and say exactly what it left out, without ever claiming the full gate.

Authority, in creation order:
- The issue body of #442 (`updated_at` 2026-11-13T07:12:46Z). The issue has no comments at the time of this revision, so no decision or approval comment changes it. It carries `ready-for-agent`; no comment names an implementing tool, so the tool is Claude Code by default (AGENTS.md "Implementer and reviewer").
- There is no split, no parent item and no item plan template entry. The whole issue is one pull request.

Cited, not authority: [#440](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440) (the gate-order issue the #442 body points to) point 2, which proposes an environment-limited local run as the pre-PR run and the hosted full gate as the pre-merge gate. Whether that is adopted is #440's question, not this plan's. [#443](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443) proposal point 3 will read `summary.json`; this is why section 3 pins its format.

`/scripts/` and `/docs/` are protected paths (`.github/CODEOWNERS` lines 5 and 13), so this pull request merges only through Hari (AGENTS.md GOVERNANCE "Merge rule").

## 2. Scope

Files this pull request may change:
- `scripts/verify-dotnet.sh`
- `scripts/test-verify-dotnet.sh`
- `docs/engineering/dotnet-verification.md`, sections "Tiers, partial runs and failure reporting" and "Diagnostics and dependency changes" only. The issue names a section "Script options"; no section of that name exists on `main` at 675f837; the option text lives in "Tiers, partial runs and failure reporting" (lines 48 to 74).
- `items/442/` (this plan, the receipt, the item state file under docs/agents/factory.md "Item state file").

Must not change: any file under `tests/` or `src/`, including every `[Trait]` attribute; `scripts/dotnet-verification.proj` and `scripts/verification/`; `.github/`; `docs/operations/`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`; `results/`; `scenarios/`; any `.csproj`, `Directory.*.props`, `global.json` or lock file. The production script must need no tool beyond what it uses on `main` (bash, coreutils, awk, grep, git, dotnet) plus `docker` and `timeout` for the probe. No `jq` or `python3` in `scripts/verify-dotnet.sh`.

## 3. External contracts

### 3.1 Unchanged

- Option names `--tier fast|all`, `--project <name>` (repeatable), `-h`/`--help`; environment variable `PARCHWELL_BUILD_MAX_CPU`; exit status 2 for an unusable option or value, checked before any `dotnet` call and before the results directory exists.
- Full-gate pass line `Verification passed. Per-project reports identify the tests executed.`, printed only when tier is `all`, no `--project` was given, no stage failed and no probe failed.
- Partial pass line, unchanged text: `Partial verification passed (tier <tier>, projects: <list or all>). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options.` Printed when no stage failed, no probe failed, and tier is `fast` or `--project` was given.
- Fail-fast stages (restore, build, Kafka XML inventory, project evaluation, scaffold inventory) and the fail-late recording of test projects and the warning control.
- The results directory `${TMPDIR:-/tmp}/parchwell-dotnet-verification.XXXXXXXX` and the exit line `Verification logs and reports: <dir>`.

### 3.2 Stage lines and `stage-timing.tsv`

UTC timestamps everywhere in this plan have the form `YYYY-MM-DDTHH:MM:SSZ` (`date -u +%Y-%m-%dT%H:%M:%SZ`).

- Success line: `<label>: passed (<n> s, <start> to <end>)`.
- Failure line: `<label>: failed (exit <status>, <n> s, <start> to <end>)`.
- `<n>` stays whole seconds from bash `SECONDS`, as on `main`. Start and end come from the clock, so `<end>` minus `<start>` can differ from `<n>` by 1 s; neither value is adjusted.
- `stage-timing.tsv`: one line per `run_logged` stage, no header, four tab-separated fields in this order: label, whole seconds, UTC start, UTC end. The first two fields keep their `main` meaning.
- Timing table: heading `Stage timing (seconds, UTC start and end), also in <path>:`, then one line per stage with the four fields.

### 3.3 Environment probes

Probes run only when tier is `all`. With `--tier fast` no probe runs, because that tier already leaves out `Tier=Docker` and `Tier=Host`. Probes run after the SDK version check and before `Locked restore`. Each prints one line:
- `Environment probe Docker: available` or `Environment probe Docker: unavailable (<reason>)`
- `Environment probe Host: available` or `Environment probe Host: unavailable (<reason>)`

Docker probe:
- Command: `timeout 30 docker version --format '{{.Server.Version}}'`, output discarded. This is the reachability check that `scripts/verify-worker-images.sh` line 17 already uses, with a 30 s bound. The 30 s bound is a policy choice, not a measurement.
- Available exactly when the command exits 0.
- Reasons, literal: `docker CLI not found` when `command -v docker` fails; otherwise `docker version exited <status>`.

Host probe:
- Reads `<proc>/cgroup` and `<proc>/mountinfo`, where `<proc>` is `${PARCHWELL_VERIFY_PROC_SELF:-/proc/self}`. `PARCHWELL_VERIFY_PROC_SELF` exists for `scripts/test-verify-dotnet.sh`; docs state that and nothing else uses it.
- Resolves the directory exactly as `B10AbruptCgroup.Resolve` does (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptCgroup.cs` lines 67 to 85): exactly one line starting `0::`; that path starts with `/` and has no `.` or `..` segment; exactly one mountinfo line whose field after the ` - ` separator is `cgroup2`; mount root is field 4 and mount point field 5, each unescaped for `\040`, `\011`, `\012`, `\134`; the path is under the mount root; the directory is the mount point joined with the path relative to the root. The probe does not reproduce the test harness's namespace-mismatch fallback (`tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessHarness.cs` lines 87 to 93).
- Available exactly when `<directory>/memory.events.local` is a readable regular file.
- Reasons, literal: `no single cgroup v2 line in <proc>/cgroup`; `invalid cgroup v2 path in <proc>/cgroup`; `no single cgroup2 mount in <proc>/mountinfo`; `cgroup v2 path outside the cgroup2 mount root`; `<directory>/memory.events.local not found`. `<proc>` and `<directory>` are printed as resolved.

### 3.4 Exclusion and the environment-limited mode

When at least one probe is unavailable, the run is environment-limited:
- Every `dotnet test` run call receives, after the arguments it has on `main`, `--filter-not-trait` followed by `Tier=Docker` if the Docker probe failed and `Tier=Host` if the Host probe failed, in that order. No other trait is excluded; `Tier=Slow` tests run.
- After `Unit scaffold inventory` and before the first test project, for each failed probe (Docker first) and each selected non-empty test project (architecture project first, then the scaffold inventory order), one fail-fast `run_logged` stage labelled `<project> Tier=<trait> listing` runs `dotnet test --project <path> --configuration Release --no-build --list-tests --filter-trait "Tier=<trait>"`. Exit 0 with a line `Discovered <k> tests.` gives `<k>`; exit 8 gives 0. Any other exit, or exit 0 without that line, stops the run with that stage's status (exit 1 for the missing line).
- `<n>` for a trait is the sum of `<k>` over the selected projects.
- Exclusion line, literal, one per failed probe, printed after the listings and again just before the final status line: `Environment-limited run: <n> Tier=<trait> tests not run because <reason>`, where `<reason>` is the probe's reason from 3.3. It is printed for `<n>` = 0 too.
- When the Docker probe failed, the spike-evidence publication (`-t:PublishSpikeEvidence`) does not run, as with `--tier fast`.
- Final status line when no stage failed, literal: `Environment-limited run finished without failures (tier all, projects: <list or all>). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options.` The run never prints `Verification passed` or `Partial verification passed`.
- Exit status when no stage failed: **open, see section 12 question 1.** When a stage failed, the run prints the failed-stage list as on `main` and exits with the first failure's status.

### 3.5 `summary.json`

Written to `<results directory>/summary.json` on every exit after the results directory is created, including fail-fast exits. Not written for the exit-2 argument checks, which happen before the directory exists. Valid JSON, UTF-8, one object. Key order and whitespace are not pinned. Keys and types:

| Key | Type and value |
|---|---|
| `schema_version` | number `1` |
| `started_utc`, `ended_utc` | string, UTC form above; start is taken before the probes, end in the exit handler |
| `source_sha` | string from `git rev-parse HEAD`, or `null` when that fails |
| `source_dirty` | `true` when `git status --porcelain` prints anything, `false` when it prints nothing, `null` when it fails |
| `sdk_version` | string, the checked SDK version |
| `tier` | `"all"` or `"fast"` |
| `projects` | `null` without `--project`; otherwise array of the given names in the given order |
| `nproc` | number, output of `nproc` |
| `mem_total_kib` | number, the `MemTotal` value in kB from `/proc/meminfo`, or `null` when absent |
| `dotnet_processor_count` | string, the `DOTNET_PROCESSOR_COUNT` value exactly as set, or `null` when unset |
| `parchwell_build_max_cpu` | string, the `PARCHWELL_BUILD_MAX_CPU` value, or `null` when unset |
| `probes` | array of two objects in order Docker, Host: `{"trait": "Docker"\|"Host", "result": "available"\|"unavailable"\|"not_run", "reason": string or null}`; `not_run` with `--tier fast` |
| `excluded` | array, one object per failed probe: `{"trait": string, "tests": number, "reason": string}`; empty array when none. `tests` is `null` when the run stopped before the listing finished |
| `stages` | array in run order, one object per `run_logged` stage: `{"label": string, "elapsed_s": number, "start_utc": string, "end_utc": string, "exit_code": number}` |
| `failed_stages` | array of `{"label": string, "exit_code": number}` recorded by `record_failure`, in order; for a fail-fast exit, the one stopping stage |
| `exit_code` | number, the script's exit status |
| `outcome` | `"full_gate_passed"`, `"partial_passed"`, `"environment_limited_passed"` or `"failed"`; set by the same conditions as the final status lines |
| `slowest_tests` | object keyed by test project name, for each project whose `results.xml` exists: array of at most 30 `{"name": string, "time_s": string}` |

`slowest_tests` rules:
- Source: every `<test>` element of that project's `results.xml`. `time` is the xUnit v2 XML attribute written from `ExecutionTime` with invariant culture ([pinned writer](https://github.com/xunit/xunit/blob/0c7aa4991fa340689155d52043ce88f52f2f7421/src/xunit.v3.runner.common/Results/Builtin/XmlV2ResultWriterMessageHandler.cs#L77-L85), lines 77 to 85; runner version 4.0.2+0c7aa4991f, observed 2026-11-13).
- Order: descending numeric `time`; equal times by `name` in ascending byte order. Keep the first 30.
- `time_s`: the attribute text exactly as written.
- `name`: the `name` attribute with `&lt;`, `&gt;`, `&quot;`, `&apos;` and `&amp;` decoded (`&amp;` last) and nothing else decoded, then JSON-escaped (`\` to `\\`, `"` to `\"`, each byte below 0x20 to `\u00XX`).

## 4. Requirements

1. WHEN tier is `all`, both probes succeed and no stage fails THEN no `--filter-not-trait` is passed, no listing stage runs, the full-gate line prints (without `--project`) and `outcome` is `full_gate_passed`, exit 0.
2. WHEN any `run_logged` stage ends THEN its console line, its `stage-timing.tsv` line and its `stages` entry carry the same label, whole seconds, UTC start and UTC end in the formats of 3.2 and 3.5.
3. WHEN the Docker probe's command exits non-zero THEN the run is environment-limited for `Tier=Docker` with reason `docker version exited <status>`, test calls exclude only `Tier=Docker`, the spike-evidence publication does not run, and `Tier=Slow` and `Tier=Host` tests run.
4. WHEN the Host probe cannot resolve a directory or `memory.events.local` is missing THEN the run is environment-limited for `Tier=Host` with the matching reason of 3.3, and test calls exclude only `Tier=Host`.
5. WHEN both probes fail THEN test calls receive `--filter-not-trait Tier=Docker Tier=Host`, and two exclusion lines print, Docker first.
6. WHEN the run is environment-limited THEN `<n>` per trait is the listing sum of 3.4, the exclusion line prints twice with that `<n>`, `Verification passed` and `Partial verification passed` never print, and with no failed stage the final line of 3.4 prints and `outcome` is `environment_limited_passed`.
7. WHEN a listing stage exits other than 0 or 8 THEN the run stops before any test project with that status, and `summary.json` exists with `outcome` `failed` and `exit_code` equal to that status.
8. WHEN tier is `fast` THEN no probe runs, no listing runs, the filter is `--filter-not-trait Tier=Docker Tier=Slow Tier=Host` as on `main`, no `Environment-limited` text prints, and both `probes` entries have `result` `not_run`.
9. WHEN a test project or the warning control fails THEN `failed_stages` lists each in order, `outcome` is `failed` and `exit_code` is the first failure's status.
10. WHEN a project's `results.xml` exists THEN `slowest_tests.<project>` holds its slowest tests by the rules of 3.5.
11. WHEN the script exits 2 on an argument or `PARCHWELL_BUILD_MAX_CPU` check THEN no results directory and no `summary.json` exist, as no directory is created before those checks on `main`.
12. WHEN `DOTNET_PROCESSOR_COUNT` or `PARCHWELL_BUILD_MAX_CPU` is set THEN `summary.json` records the value as given, and the script does not set or change `DOTNET_PROCESSOR_COUNT` (docs/engineering/dotnet-verification.md line 72).
13. The usage text adds these lines after the `A run with --tier fast or --project` line, literal:
    ```
    With --tier all the script first checks for a Docker daemon and for memory.events.local in the
    process's cgroup v2 directory. A failed check leaves out only the matching Tier=Docker or Tier=Host
    tests, names them, and makes the run environment-limited, which is not the full gate.
    Each run writes summary.json and stage-timing.tsv to its results directory.
    ```

## 5. Invariants and safety rules

- With both probes available, the set of tests run under each option is exactly the set on `main`.
- An environment-limited run never prints a line containing `Verification passed` and never sets `outcome` to `full_gate_passed` or `partial_passed`.
- Exclusion is only by an existing trait. No test is skipped, deleted, retagged or loosened; no file under `tests/` changes.
- `summary.json` holds no secret, path to a credential, host name or environment variable other than the two named in 3.5 (AGENTS.md hard safety rule 7). Test names and the results directory path are the only free text, besides probe reasons.
- No workflow changes (AGENTS.md hard safety rule 8). Hosted CI keeps calling `./scripts/verify-dotnet.sh` with no options (`.github/workflows/dotnet-verification.yml` line 27).

## 6. Standing fixture rule

This plan adds checks (the probes) that change what the stub-based cases in `scripts/test-verify-dotnet.sh` see: on a machine without Docker or the cgroup file, the existing `default`, `fast`, `fail_late`, `select`, `cpu` and `warning_accepted` cases would now get a filter. When an existing case's fixture is affected by a new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the case exercises. Expected here: put a stub `docker` that exits 0 first on `PATH`, set `PARCHWELL_VERIFY_PROC_SELF` to the passing fixture of section 7, and make the stub `dotnet` log listing calls as `LIST` lines rather than `TEST` lines, so `tested` keeps counting run calls only;
- (b) where invalid input is the case's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each changed case with the check that forces it, and the cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Adding assertions to an existing case is allowed.

## 7. Test map

All tests are cases in `scripts/test-verify-dotnet.sh`, which keeps needing no SDK and no Docker. It may use `jq` to read `summary.json` and states that prerequisite in its header comment and in the docs; the production script does not use `jq`. The results directory is taken from the `Verification logs and reports:` line.

Fixtures (created under the test's `$work`):
- `proc-ok/cgroup`: `0::/test.slice`; `proc-ok/mountinfo`: `30 25 0:26 / $work/cgroupfs rw,nosuid - cgroup2 cgroup2 rw`; file `$work/cgroupfs/test.slice/memory.events.local` containing `oom_kill 0`.
- `proc-hybrid/cgroup`: `0::/`; `proc-hybrid/mountinfo`: `46 36 0:38 / $work/unified rw,relatime - cgroup2 cgroup2 rw`; directory `$work/unified` without `memory.events.local`. This copies the hybrid layout observed in this planning session (section 8, A2).
- `proc-two-mounts/mountinfo`: two `cgroup2` lines; `cgroup` as in `proc-ok`.
- Stub `docker`: exits `${STUB_DOCKER_STATUS:-0}`.
- Stub `dotnet`, added behaviour: a `test` call containing `--list-tests` logs `LIST <name> <args>`, prints `Discovered ${STUB_LIST_COUNT:-2} tests.` and exits 0, or exits 8 after printing `Discovered 0 tests.` when `STUB_LIST_COUNT=0`, or exits `STUB_LIST_STATUS` when set; a non-list `test` call copies `$STUB_REPORT` to `<results-directory>/results.xml` when `STUB_REPORT` is set.
- Report fixture `slow.xml`: 32 `<test>` elements: `T01` to `T30` with `time` `0.001` to `0.030` (two-digit index over 1000), `T30b` with `time` `0.030`, and one with name attribute `Q &quot;quoted&quot; &amp; back\slash` and `time` `12.5`.

| Req | Case | Inputs | Literal expectations |
|---|---|---|---|
| 1 | `default` (existing, fixture changed under 6(a)) | proc-ok, docker 0 | exit 0; 6 `TEST` lines; 0 `LIST` lines; no `filter-not-trait`; full-gate line; `outcome` `full_gate_passed`; `excluded` `[]`; both probes `available` |
| 2 | `default` | as above | every `stage-timing.tsv` line has 4 tab fields, fields 3 and 4 match `^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$`; output has a line matching `^Locked restore: passed \([0-9]+ s, <ts> to <ts>\)$`; `stages[0].label` `Locked restore`; number of `stages` equals number of tsv lines |
| 3 | `docker_down` | proc-ok, `STUB_DOCKER_STATUS=1` | exit per question 1; 6 `TEST` lines each containing `--filter-not-trait Tier=Docker` and none containing `Tier=Host` or `Tier=Slow`; 6 `LIST` lines each with `--filter-trait Tier=Docker`; output contains `Environment-limited run: 12 Tier=Docker tests not run because docker version exited 1` exactly twice; no `PublishSpikeEvidence` in the log; `excluded` `[{"trait":"Docker","tests":12,"reason":"docker version exited 1"}]` |
| 4 | `host_down` | proc-hybrid, docker 0 | `TEST` lines contain `--filter-not-trait Tier=Host` and not `Tier=Docker`; output contains `Environment-limited run: 12 Tier=Host tests not run because $work/unified/memory.events.local not found` twice; `PublishSpikeEvidence` present in the log |
| 4 | `host_two_mounts` | proc-two-mounts, docker 0 | output contains `Environment probe Host: unavailable (no single cgroup2 mount in $work/proc-two-mounts/mountinfo)` |
| 5 | `both_down` | proc-hybrid, `STUB_DOCKER_STATUS=1` | every `TEST` line contains `--filter-not-trait Tier=Docker Tier=Host`; 12 `LIST` lines; the Docker exclusion line precedes the Host exclusion line |
| 6 | `docker_down`, `both_down` | as above | output contains no `Verification passed` and no `Partial verification passed`; contains `Environment-limited run finished without failures (tier all, projects: all). This is not the full gate:`; `outcome` `environment_limited_passed` |
| 6 | `list_zero` | proc-ok, `STUB_DOCKER_STATUS=1`, `STUB_LIST_COUNT=0` | output contains `Environment-limited run: 0 Tier=Docker tests not run because docker version exited 1` |
| 6 | `limited_select` | proc-hybrid, docker 0, `--project Parchwell.Scenarios.Tests` | 1 `TEST` line; 1 `LIST` line; `Environment-limited run: 2 Tier=Host`; final line contains `projects: Parchwell.Scenarios.Tests`; no `Partial verification passed` |
| 7 | `list_fail` | proc-ok, `STUB_DOCKER_STATUS=1`, `STUB_LIST_STATUS=3` | exit 3; 0 `TEST` lines; `summary.json` exists with `outcome` `failed`, `exit_code` 3, `excluded[0].tests` `null` |
| 8 | `fast` (existing, assertions added) | proc-hybrid, `STUB_DOCKER_STATUS=1`, `--tier fast` | existing assertions hold; 0 `LIST` lines; no `Environment` text in output; both `probes[].result` `not_run` |
| 9 | `fail_late` (existing, fixture changed under 6(a), assertions added) | proc-ok, docker 0 | existing assertions hold; `failed_stages` labels `Parchwell.Kafka.Tests`, `Parchwell.Verifier.Tests` with `exit_code` 2; `outcome` `failed`; `exit_code` 2 |
| 10 | `slowest` | proc-ok, docker 0, `STUB_REPORT=slow.xml` | for each of the 6 projects: 30 entries; entry 0 `{"name":"Q \"quoted\" & back\\slash","time_s":"12.5"}` (raw JSON text); entries 1 and 2 names `T30`, `T30b`; entry 29 name `T03`; no entry `T01` or `T02` |
| 11 | `unknown_project`, `unknown_tier`, `bad_cpu` (existing) | as on `main` | existing assertions hold; no `parchwell-dotnet-verification.*` directory is created under the test's `TMPDIR` |
| 12 | `cpu` (existing, fixture changed under 6(a), assertions added) | `PARCHWELL_BUILD_MAX_CPU=3`, `DOTNET_PROCESSOR_COUNT=2` | existing assertions hold; `parchwell_build_max_cpu` `"3"`; `dotnet_processor_count` `"2"`; in `default`, both are `null` |
| 3.5 | `default` | | `schema_version` 1; `source_sha` equals `git rev-parse HEAD`; `nproc` equals `nproc`; `mem_total_kib` equals the `/proc/meminfo` `MemTotal` number; `tier` `all`; `projects` `null`; `sdk_version` `10.0.402` |
| 13 | `help` | `--help` | exit 0; output contains the four literal lines of requirement 13 |

Boundaries not covered by an automated case, with reason:
- `docker CLI not found`: the test cannot remove a real `docker` from `PATH` without also removing bash and coreutils. The receipt records one run with `PATH` set to a directory of symlinks that holds no `docker`.
- The 30 s Docker timeout (`docker version exited 124`): a case would take 30 s. Not run; the reason text follows from coreutils `timeout`, which exits 124 when the command times out (`timeout --help`, read in the implementing session and cited in the receipt).
- Mount root other than `/` and escaped mount points: covered by the C# `Resolve` tests only for the C# code. The implementer adds one stub case for a mount root `/test.slice` with cgroup `0::/test.slice/inner`, expecting directory `<mount point>/inner`, if it fits the case table; otherwise the receipt records why not.

## 8. Deployment assumptions

None of these involves a network connection to Azure or Kafka; plan rule 3's open-or-fresh connection and idle-or-load conditions apply only as stated.

- A1: `docker version --format '{{.Server.Version}}'` exits non-zero when no daemon is reachable. Evidence: one run in this planning session on 2026-11-13, an idle sandbox with `/usr/bin/docker` and no `/var/run/docker.sock`, a fresh connection attempt: exit 1, `failed to connect to the docker API at unix:///var/run/docker.sock`. The hosted runner reaches Docker with the same command: [run 43307845372](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43307845372), step `Verify worker images start` succeeded, and that script stops at the same check when it fails. Offline test: `docker_down` (stub).
- A2: in this planning sandbox on 2026-11-13 (idle), `/proc/self/cgroup` has `0::/`, the single `cgroup2` mount is `/sys/fs/cgroup/unified` with root `/`, and `/sys/fs/cgroup/unified/memory.events.local` does not exist. Matches the issue's measurement. Offline test: `host_down` (stub fixture copied from it).
- A3: the `dotnet test` processes are in the same cgroup as the script's bash process, so the bash probe checks the test's directory. Basis: Linux cgroup v2 documentation, "When a process forks a child process, the new process is born into the cgroup that the forking process belongs to at the time of the operation" ([docs.kernel.org cgroup-v2](https://docs.kernel.org/admin-guide/cgroup-v2.html), "Processes", read 2026-11-13). Inferred: no process manager in between moves the test host. Not tested.
- A4: `dotnet test --project <p> --configuration Release --no-build --list-tests --filter-trait "Tier=<t>"` on SDK 10.0.402 lists the matching tests and prints `Discovered <k> tests.`; with no match it prints `Discovered 0 tests.` and exits 8. Evidence: run in this planning session on `tests/Parchwell.LoadGen.Tests` at 675f837: `Tier=Host` 1, `Tier=Docker` 8, `Tier=Nope` exit 8. Exit 8 means the session discovered no tests ([MTP exit codes](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes)); `--list-tests` lists without running and is passed directly to `dotnet test` ([MTP CLI options](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-cli-options#platform-options), [dotnet test with MTP](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp#whole-run-and-per-module-minimums)). The output line format is observed, not documented. Offline tests use a stub; acceptance command 2 records a real listing.
- A5: on the hosted `ubuntu-24.04` runner, the Host probe's resolution finds `memory.events.local`. Not verified: the `Tier=Host` test passes there today, but it may pass through the harness fallback that the probe does not reproduce. **Listed under section 12 question 2** until Hari accepts the risk by name.

## 9. Acceptance commands

1. `./scripts/test-verify-dotnet.sh`: exits 0 and prints `verify-dotnet.sh checks passed.` Evidence: output in the receipt, with the case count.
2. `./scripts/verify-dotnet.sh --project Parchwell.LoadGen.Tests` in a sandbox without Docker and without `memory.events.local`: the Docker and Host probe lines show `unavailable`; the exclusion lines show the real counts and reasons; the run ends with the environment-limited final line; `summary.json` parses with `jq -e .` and has `outcome` `environment_limited_passed` or `failed`, and the receipt states which and why. Evidence: the probe, exclusion and final lines, and `jq` output of `excluded` and `outcome`, in the receipt.
3. `./scripts/verify-dotnet.sh` with no options in the same sandbox: an environment-limited run, recorded as such in the receipt and never as the full gate.
4. Hosted `.NET verification` on the pull request's final head: passes, and its log shows `Environment probe Docker: available` and `Environment probe Host: available` and the full-gate line. If either probe shows `unavailable`, the implementer stops under section 13.
5. `npm run check:docs`: no warning in a changed file, or each disposed of under docs/agents/pull-requests.md rule 8.

## 10. Sources

- `scripts/verify-dotnet.sh` on `main` at 675f837277824d197ce922511b9f55c7d77623b2: lines 7 to 20 (usage), 55 to 62 (tiers), 102 to 126 (results directory and `run_logged`), 137 to 170 (`run_tests`, spike evidence), 249 to 283 (scaffold inventory, loop, final lines).
- `scripts/test-verify-dotnet.sh` at the same commit, lines 11 to 65 (stub and `run_case`) and 67 to 107 (cases).
- `scripts/verify-worker-images.sh` line 17 (Docker check).
- `docs/engineering/dotnet-verification.md` at the same commit, lines 48 to 74 and 107 to 109.
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptCgroup.cs` lines 15 to 51 and 67 to 85; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessHarness.cs` lines 82 to 97; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs` lines 12 to 26.
- `.github/workflows/dotnet-verification.yml` lines 26 to 33; `.github/CODEOWNERS`.
- xUnit v2 XML writer at 0c7aa4991fa340689155d52043ce88f52f2f7421, lines 77 to 85 (link in 3.5), fetched with curl.
- Microsoft Learn pages in A4, read 2026-11-13 with the Microsoft Learn documentation search tool.
- Linux cgroup v2 documentation (A3), fetched with curl 2026-11-13.
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on (plan rule 1 has no case here).

## 11. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the listing stages scale with selected projects times failed probes, bounded by the scaffold inventory (6 projects today, at most 12 listings). `slowest_tests` is capped at 30 per report. Implementation effort has no runtime-sized step: the existing case list is fixed (9 cases at 675f837) and the new cases are listed above. No cap is needed.

(b) Stricter than a parent plan: there is no parent plan. Against the issue body, the plan adds: `summary.json` also on fail-fast exits; listing failures stop the run; `source_dirty`; `outcome`; a 30 s bound on the Docker probe; probes only with `--tier all`; an exclusion line also when `<n>` is 0. Each keeps the run from reporting more than it checked.

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: the Docker CLI and daemon (A1), the kernel's cgroup layout (A2, A3), the SDK's listing output (A4) and the hosted runner's cgroup layout (A5). A1, A2 and A4 are checked against recorded output above; A3 against the kernel documentation, with the inferred part named; A5 is not settled and is section 12 question 2.

## 12. Open questions for Hari

1. Exit status of an environment-limited run in which no stage failed. The issue says the mode "never satisfies the full gate", and hosted CI treats exit 0 of `./scripts/verify-dotnet.sh` as the gate (`.github/workflows/dotnet-verification.yml` line 27). Options, none chosen here: (A) exit 0, relying on the printed line and `outcome`, so a runner whose probe fails would turn the required check green without the excluded tests; (B) a dedicated non-zero status, for example 3, so hosted CI fails and a local session reads `outcome`; (C) the mode only with a new explicit option, and without it a failed probe changes nothing, so the excluded tests run and fail visibly as on `main`. Option C changes the issue's proposal 2 and adds a CLI flag whose name you would choose. Which one?
2. Accept by name the risk of deployment assumption A5 (the hosted runner passes the Host probe), or require evidence first. Acceptance command 4 checks it on the pull request's own hosted run, before merge; whether that check is enough depends on your answer to question 1.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a literal expectation in section 7, or when acceptance command 4 shows a probe `unavailable` on the hosted runner.
