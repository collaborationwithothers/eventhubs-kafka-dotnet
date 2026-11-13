# Plan: verification run summary

Issue: [#442](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442). Pull request short name: `verify-run-summary`. Revision 2.

This plan adds UTC start and end stamps to every stage of `scripts/verify-dotnet.sh` and a `summary.json` run summary with machine facts, exit statuses and the 30 slowest tests of each report. It does not add environment probes or an environment-limited mode: Hari's decision of 2026-11-13 dropped them, so a run without Docker or without cgroup v2 files fails visibly, as on `main`. Which tests run, the pass lines and the exit status stay as on `main`.

## 1. Goal and authority

Goal: a verification run records when each stage ran, on what machine, with what result, and which tests were slowest, in a file a later tool can read.

Authority, in creation order:
- The issue body of #442. The issue's `updated_at` is 2026-11-13T08:23:11Z, the creation time of the decision below; the REST API gives no separate body edit time. `ready-for-agent` was applied at 2026-11-13T07:12:46Z (issue events). No comment names an implementing tool, so the tool is Claude Code by default (AGENTS.md "Implementer and reviewer").
- [Decision 6847884649](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6847884649) (2026-11-13T08:23:11Z), answering the handoff on Revision 1: drop the environment-limited mode and both probes; keep the UTC stage stamps, `summary.json` without the `probes` and `excluded` keys, and the slowest-tests list; leave the full-gate sentence of `docs/engineering/dotnet-verification.md` line 50 unchanged; revise, check, implement. It notes that [#445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445) renames `Tier=Host` to `Tier=CGroupV2`, and whichever pull request merges second uses the new name wherever this issue's change refers to the trait.
- There is no split, no parent item and no item plan template entry. The whole issue is one pull request.

Not authority: the Revision 1 plan comment, [plan check 6846964024](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6846964024) and [handoff 6846974974](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6846974974). This revision applies the check's findings that survive the decision; section 14 lists them. [#443](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443) proposal point 3 will read `summary.json`; this is why section 3 pins its format.

`/scripts/` and `/docs/` are protected paths (`.github/CODEOWNERS` lines 5 and 13), so this pull request merges only through Hari (AGENTS.md GOVERNANCE "Merge rule").

Trait rename: no contract, requirement or literal expectation in this plan names a `Tier` trait value, and the change edits no line that names one (the fast-tier filter at `scripts/verify-dotnet.sh` line 57, the usage line 13, the `fast` case at `scripts/test-verify-dotnet.sh` lines 76 to 77 and the trait table in `docs/engineering/dotnet-verification.md` lines 60 to 66 stay as they are on the base). If #445 merges first, the implementer rebases and keeps #445's names in those lines.

## 2. Scope

Files this pull request may change:
- `scripts/verify-dotnet.sh`
- `scripts/test-verify-dotnet.sh`
- `docs/engineering/dotnet-verification.md`, section "Tiers, partial runs and failure reporting" only, at the two paragraphs section 3.6 names. The issue names a section "Script options"; no section of that name exists on `main` at 2077964baaa918ccf7c671b5c2370511a10cc66f; the option text lives in "Tiers, partial runs and failure reporting" (lines 48 to 74). Line 50, the full-gate sentence, does not change.
- `items/442/` (this plan, the receipt, the item state file under docs/agents/factory.md "Item state file").

Must not change: any file under `tests/` or `src/`, including every `[Trait]` attribute; `scripts/dotnet-verification.proj` and `scripts/verification/`; `.github/`; `docs/operations/`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`; `results/`; `scenarios/`; any `.csproj`, `Directory.*.props`, `global.json` or lock file.

Tools: on `main`, `scripts/verify-dotnet.sh` uses bash, coreutils (`mktemp`, `mkdir`, `cp`, `cat`, `tail`, `basename`, `dirname`), `awk`, `grep` and `dotnet`; it does not call `git`, `jq`, `timeout` or `nproc` (`git grep -n -E "\b(git|jq|timeout|nproc)\b" origin/main -- scripts/verify-dotnet.sh` prints nothing). This change adds `git`, used only for `source_sha` and `source_dirty`, which are `null` when it fails, and the coreutils `date` and `nproc`. It adds no other tool; in particular no `jq` and no `python3` in `scripts/verify-dotnet.sh`.

## 3. External contracts

### 3.1 Unchanged

- Option names `--tier fast|all`, `--project <name>` (repeatable), `-h`/`--help`; environment variable `PARCHWELL_BUILD_MAX_CPU`; exit status 2 for an unusable option or value, checked before any `dotnet` call and before the results directory exists.
- The fast-tier filter, and the set of tests each option runs.
- Full-gate pass line `Verification passed. Per-project reports identify the tests executed.` and partial pass line `Partial verification passed (tier <tier>, projects: <list or all>). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options.`, with the conditions of `scripts/verify-dotnet.sh` lines 279 to 283.
- Fail-fast stages (restore, build, Kafka XML inventory, project evaluation, scaffold inventory), the fail-late recording of test projects and the warning control, the failed-stage list and the exit status of the first failure (lines 274 to 278).
- The results directory `${TMPDIR:-/tmp}/parchwell-dotnet-verification.XXXXXXXX`, and the exit line `Verification logs and reports: <dir>`, which stays the last line the script prints.
- The script's exit status in every case. Writing `summary.json` never changes it.

### 3.2 Stage lines and `stage-timing.tsv`

UTC timestamps everywhere in this plan have the form `YYYY-MM-DDTHH:MM:SSZ` (`date -u +%Y-%m-%dT%H:%M:%SZ`).

- Success line: `<label>: passed (<n> s, <start> to <end>)`.
- Failure line: `<label>: failed (exit <status>, <n> s, <start> to <end>)`.
- `<n>` stays whole seconds from bash `SECONDS`, as on `main`. Start and end come from the clock, so `<end>` minus `<start>` can differ from `<n>` by 1 s; neither value is adjusted.
- `stage-timing.tsv`: one line per `run_logged` stage, no header, four tab-separated fields in this order: label, whole seconds, UTC start, UTC end. The first two fields keep their `main` meaning.
- Timing table: heading `Stage timing (seconds, UTC start and end), also in <path>:`, then one line per stage with the four fields.

### 3.3 Usage text

The usage text adds one line after the `A run with --tier fast or --project prints that it is not the full gate.` line, literal:

```
Each run writes summary.json and stage-timing.tsv to its results directory.
```

### 3.4 `summary.json`

Written to `<results directory>/summary.json` on every exit after the results directory is created, including fail-fast exits and exits from an unexpected failing command. Not written on any exit before the directory exists: the exit-2 option checks, `--help`, and the SDK checks at lines 89 to 100. Valid JSON, UTF-8, one object. Key order and whitespace are not pinned. Keys and types:

| Key | Type and value |
|---|---|
| `schema_version` | number `1` |
| `started_utc`, `ended_utc` | string, UTC form above; start is taken when the results directory is created, end when the summary is written at exit |
| `source_sha` | string from `git rev-parse HEAD` in the repository root, or `null` when that command fails |
| `source_dirty` | `true` when `git status --porcelain` prints anything, `false` when it prints nothing, `null` when it fails |
| `sdk_version` | string, the checked SDK version |
| `tier` | `"all"` or `"fast"` |
| `projects` | `null` without `--project`; otherwise array of the given names in the given order |
| `nproc` | number, output of `nproc` |
| `mem_total_kib` | number, the `MemTotal` value in kB from `/proc/meminfo`, or `null` when that line is absent or the file cannot be read |
| `dotnet_processor_count` | string, the `DOTNET_PROCESSOR_COUNT` value exactly as set, or `null` when unset |
| `parchwell_build_max_cpu` | string, the `PARCHWELL_BUILD_MAX_CPU` value exactly as set, or `null` when unset or empty |
| `stages` | array in run order, one object per `run_logged` stage: `{"label": string, "elapsed_s": number, "start_utc": string, "end_utc": string, "exit_code": number}` |
| `failed_stages` | array of `{"label": string, "exit_code": number}` in order: each `record_failure` call with its label as on `main` (a test project name or `Warning control`), or, for a fail-fast exit, one entry: the stopping `run_logged` stage's label, or `Project evaluation` for the project evaluation loop (lines 234 to 246); empty array otherwise |
| `exit_code` | number, the script's exit status |
| `outcome` | `"full_gate_passed"` when the full-gate line printed, `"partial_passed"` when the partial line printed, `"failed"` otherwise |
| `slowest_tests` | object keyed by test project name, one key for each project whose `<results directory>/<project>/results.xml` exists at exit: array of at most 30 `{"name": string, "time_s": string}`; `{}` when no report exists |

String values are JSON-escaped: `\` to `\\`, `"` to `\"`, each byte below 0x20 to `\u00XX` with lowercase or uppercase hex. Nothing else is escaped.

When the summary cannot be written, the script prints `summary.json could not be written.` to standard error, before the exit line, and keeps its exit status.

`slowest_tests` rules:
- Source: every `<test>` element of that project's `results.xml`. The pinned xUnit writer sets `name` to `XmlUtility.Escape(TestDisplayName)` and `time` to `ExecutionTime` formatted with invariant culture ([XmlV2ResultWriterMessageHandler.cs lines 79 and 81](https://github.com/xunit/xunit/blob/0c7aa4991fa340689155d52043ce88f52f2f7421/src/xunit.v3.runner.common/Results/Builtin/XmlV2ResultWriterMessageHandler.cs#L79-L81)). `Escape` writes `\` as `\\`, `"` as `\"` and each character below 32 as a backslash sequence ([XmlUtility.cs lines 33 to 49](https://github.com/xunit/xunit/blob/0c7aa4991fa340689155d52043ce88f52f2f7421/src/xunit.v3.runner.common/Utility/XmlUtility.cs#L33-L49)). So `name` holds xUnit's escaped display name, not the display name.
- Order: descending numeric `time`; equal times by `name` in ascending byte order. Keep the first 30.
- `time_s`: the attribute text exactly as written.
- `name`: the attribute text with `&lt;`, `&gt;`, `&quot;`, `&apos;` and `&amp;` decoded, `&amp;` last, and nothing else decoded; a numeric character reference stays as written. Then JSON-escaped as above.

### 3.5 Free text in `summary.json`

The only free text is: test names, stage labels (fixed text and test project names), the two environment variable values, the `--project` names and the SDK version. No path, host name or other environment variable is written (AGENTS.md hard safety rule 7).

### 3.6 Documentation wording

In `docs/engineering/dotnet-verification.md`, section "Tiers, partial runs and failure reporting":
- Line 70: the last sentence, "It writes the same table to `stage-timing.tsv` in its results directory.", becomes, literal: "Each stage line also gives the stage's UTC start and end. The script writes the same table to `stage-timing.tsv` in its results directory, one line per stage with four tab-separated fields: label, whole seconds, UTC start and UTC end."
- A new paragraph after line 70 and before the `PARCHWELL_BUILD_MAX_CPU` paragraph, literal opening: "On every exit after it creates its results directory, the script also writes `summary.json` there. Writing it never changes the exit status. It records the run's UTC start and end, the source commit and whether the working tree had changes, the tier and selected projects, the processor count from `nproc`, total memory from `/proc/meminfo`, the values of `DOTNET_PROCESSOR_COUNT` and `PARCHWELL_BUILD_MAX_CPU` as set, each stage with its exit status, the failed stages, the exit status, the outcome, and the 30 slowest tests of each test project's report. A test name there is xUnit's escaped display name, as the report holds it." Then a table with the Key and Type columns of section 3.4, copied verbatim.
- Line 74 becomes, literal: "`./scripts/test-verify-dotnet.sh` checks the option handling, fail-late behaviour, stage timing and `summary.json` of the script against a stub `dotnet`. It needs no SDK and no Docker, but needs `jq` to read `summary.json`. It ran in 1.5 s in the one measured run (`items/435/receipt.md`), before the stage timing and summary checks were added."

## 4. Requirements

1. WHEN any `run_logged` stage ends THEN its console line, its `stage-timing.tsv` line and its `stages` entry carry the same label, whole seconds, UTC start and UTC end in the formats of 3.2 and 3.4, and the entry's `exit_code` is the stage's status.
2. WHEN the run passes with no options THEN the full-gate line prints, exit 0, `outcome` is `full_gate_passed`, `failed_stages` is `[]`, and the test calls are exactly those on `main` (no `--filter-not-trait`).
3. WHEN the run passes with `--tier fast` or `--project` THEN the partial line prints, exit 0, `outcome` is `partial_passed`, and `tier` and `projects` record the options.
4. WHEN a test project or the warning control fails THEN `failed_stages` lists each in order with the labels of 3.4, `outcome` is `failed`, and `exit_code` is the first failure's status.
5. WHEN a fail-fast `run_logged` stage fails THEN the run stops as on `main` with that status, and `summary.json` exists with `failed_stages` holding that one stage's label and status, `outcome` `failed`, and `exit_code` that status.
6. WHEN the project evaluation loop stops the run THEN `summary.json` exists with `failed_stages` `[{"label":"Project evaluation","exit_code":<status>}]` and `exit_code` that status.
7. WHEN the script exits before the results directory exists THEN no `parchwell-dotnet-verification.*` directory is created under `TMPDIR`, as on `main`.
8. WHEN a project's `results.xml` exists at exit THEN `slowest_tests.<project>` holds its slowest tests by the rules of 3.4.
9. WHEN `DOTNET_PROCESSOR_COUNT` or `PARCHWELL_BUILD_MAX_CPU` is set THEN `summary.json` records the value as given, JSON-escaped, and the script does not set or change `DOTNET_PROCESSOR_COUNT` (docs/engineering/dotnet-verification.md line 72). WHEN unset THEN the key is `null`.
10. WHEN `git rev-parse HEAD` or `git status --porcelain` fails THEN `source_sha` or `source_dirty` is `null`, and the run continues with its exit status unchanged.
11. The usage text carries the line of 3.3.

## 5. Invariants and safety rules

- Under each option, the set of tests run, the pass lines and the exit status are exactly those on `main`.
- No test is skipped, deleted, retagged or loosened; no file under `tests/` changes.
- `summary.json` holds only the free text of 3.5 (AGENTS.md hard safety rule 7).
- No workflow changes (AGENTS.md hard safety rule 8). Hosted CI keeps calling `./scripts/verify-dotnet.sh` with no options (`.github/workflows/dotnet-verification.yml` line 27).

## 6. Standing fixture rule

This plan adds output checks to `scripts/test-verify-dotnet.sh` and changes its harness (section 7). When an existing case's fixture is rejected by a new check or affected by a harness change, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the case exercises;
- (b) where invalid input is the case's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each changed case with the check that forces it, and the cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Adding assertions to an existing case is allowed. Expected changes under (a), from section 7: the `run_case` environment line and the temporary directory of the three existing exit-2 cases.

## 7. Test map

All tests are cases in `scripts/test-verify-dotnet.sh`, which keeps needing no SDK and no Docker. It uses `jq` to read `summary.json`; when `command -v jq` fails it prints `jq is required to read summary.json.` and exits 1 before any case, and its header comment states the prerequisite. The results directory is taken from the `Verification logs and reports:` line.

Harness changes:
- `run_case` (line 60) starts `env -u DOTNET_PROCESSOR_COUNT -u PARCHWELL_BUILD_MAX_CPU PATH="$work/bin:$PATH" TMPDIR="$work"`, so a caller's values cannot reach a case; a case's own assignments still follow and apply.
- Before each of `unknown_project`, `unknown_tier`, `bad_cpu` and the new `help` case, the test runs `mkdir "$work/tmp-<case>"` and passes `TMPDIR="$work/tmp-<case>"` as an environment entry; GNU `env` applies assignments in order, so the later one wins.
- Stub `dotnet`: a `test` call copies `$STUB_REPORT` to `<value of --results-directory>/results.xml` when `STUB_REPORT` is set; a `build` call whose arguments contain `Parchwell.slnx` exits `${STUB_BUILD_STATUS:-0}`; an `msbuild` call containing `-getProperty:` exits `${STUB_EVAL_STATUS:-0}`. Existing behaviour stays.

Report fixtures (created under `$work`, each a single line, `<test>` attributes in the writer's order `id`, `name`, `result`, `time`):
- `slow.xml`: 34 `<test>` elements: `T01` to `T30` with `time` `0.001` to `0.030` (index over 1000, three decimals); `T30b` with `time` `0.030`; `Nine` with `time` `9.5`; name attribute `Angle &lt;a&gt; &apos;b&apos; &amp;lt;` with `time` `0.5`; name attribute `Q \&quot;quoted\&quot; &amp; back\\slash` (the writer's form of display name `Q "quoted" & back\slash`) with `time` `12.5`.
- `one.xml`: one `<test>` element, name `Only`, `time` `0.25`.

| Req | Case | Inputs | Literal expectations |
|---|---|---|---|
| 1 | `default` (existing, assertions added) | no options | every `stage-timing.tsv` line has 4 tab fields, fields 3 and 4 match `^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$`; output has a line matching `^Locked restore: passed \([0-9]+ s, <ts> to <ts>\)$` with `<ts>` that pattern; output has the line `Stage timing (seconds, UTC start and end), also in <dir>/stage-timing.tsv:`; `stages[0].label` `Locked restore`; number of `stages` equals number of tsv lines; every `stages[].exit_code` `0` |
| 2 | `default` | as above | existing assertions hold; `outcome` `full_gate_passed`; `exit_code` 0; `failed_stages` `[]`; `slowest_tests` `{}`; `schema_version` 1; `source_sha` equals `git -C "$repository_root" rev-parse HEAD`; `source_dirty` equals `true` exactly when `git -C "$repository_root" status --porcelain` prints anything; `nproc` equals `nproc`; `mem_total_kib` equals the `/proc/meminfo` `MemTotal` number; `tier` `all`; `projects` `null`; `sdk_version` `10.0.402`; `dotnet_processor_count` and `parchwell_build_max_cpu` `null`; started and ended match the UTC pattern; the last output line starts `Verification logs and reports: ` |
| 3 | `fast` (existing, assertions added) | `--tier fast` | existing assertions hold; `outcome` `partial_passed`; `tier` `fast`; `projects` `null` |
| 3 | `select` (existing, assertions added) | `--project Parchwell.Scenarios.Tests` | existing assertions hold; `outcome` `partial_passed`; `projects` `["Parchwell.Scenarios.Tests"]` |
| 4 | `fail_late` (existing, assertions added) | `STUB_FAIL="Parchwell.Kafka.Tests Parchwell.Verifier.Tests"` | existing assertions hold; `failed_stages` `[{"label":"Parchwell.Kafka.Tests","exit_code":2},{"label":"Parchwell.Verifier.Tests","exit_code":2}]` (compared with `jq -c`); `outcome` `failed`; `exit_code` 2; the `stages` entry labelled `Parchwell.Kafka.Tests tests` has `exit_code` 2 |
| 4 | `warning_accepted` (existing, assertions added) | `STUB_ACCEPT_WARNING=1` | existing assertions hold; last `failed_stages` label `Warning control`, `exit_code` 1; `outcome` `failed` |
| 5 | `build_fail` (new) | `STUB_BUILD_STATUS=4` | exit 4; 0 `TEST` lines; `failed_stages` `[{"label":"Solution build","exit_code":4}]`; last `stages` entry label `Solution build`, `exit_code` 4; `outcome` `failed`; `exit_code` 4; output has a line matching `^Solution build: failed \(exit 4, [0-9]+ s, <ts> to <ts>\)$` |
| 6 | `eval_fail` (new) | `STUB_EVAL_STATUS=5` | exit 5; 0 `TEST` lines; `failed_stages` `[{"label":"Project evaluation","exit_code":5}]`; `exit_code` 5; `outcome` `failed` |
| 7 | `unknown_project`, `unknown_tier`, `bad_cpu` (existing, fixture changed under 6(a)) | as on `main`, with their own `TMPDIR` | existing assertions hold; `[[ -z "$(ls -A "$work/tmp-<case>")" ]]` |
| 7 | `help` (new) | `--help` | exit 0; output contains `Each run writes summary.json and stage-timing.tsv to its results directory.`; no entry in `$work/tmp-help` |
| 8 | `slowest` (new) | `STUB_REPORT=slow.xml` | for each of the 6 projects: 30 entries; `jq -c '.slowest_tests[$p][0] \| {name, time_s}'` prints `{"name":"Q \\\"quoted\\\" & back\\\\slash","time_s":"12.5"}`; entry 1 name `Nine`, `time_s` `9.5` (numeric, not text, order); entry 2 name, read with `jq -r`, `Angle <a> 'b' &lt;`; entries 3 and 4 names `T30`, `T30b`; entry 29 name `T05`; no entry `T01` to `T04` |
| 8 | `slowest_one` (new) | `STUB_REPORT=one.xml`, `--project Parchwell.Scenarios.Tests` | `slowest_tests` has exactly the key `Parchwell.Scenarios.Tests`, holding `[{"name":"Only","time_s":"0.25"}]` (compared with `jq -c`) |
| 9 | `cpu` (existing, assertions added) | `PARCHWELL_BUILD_MAX_CPU=3`, `DOTNET_PROCESSOR_COUNT=2` | existing assertions hold; `parchwell_build_max_cpu` `"3"`; `dotnet_processor_count` `"2"`; no `TEST` line and no other stub call receives an argument that sets `DOTNET_PROCESSOR_COUNT` |
| 9 | `cpu_text` (new) | `DOTNET_PROCESSOR_COUNT` set to the bytes `a"b\c`, a tab, `d` | `jq -e .` succeeds on the file; `jq -r .dotnet_processor_count` prints those exact bytes |
| 10 | `no_git` (new) | `GIT_DIR="$work/nogit"` (a path that does not exist) | exit 0; `source_sha` `null`; `source_dirty` `null`; full-gate line prints |
| 11 | `help` | as above | as above |

Boundaries not covered by an automated case, with reason:
- `mem_total_kib` `null`: the script reads the fixed path `/proc/meminfo`, which a test cannot hide without a test-only variable; this plan adds none. The receipt states the code path.
- `summary.json could not be written.`: a test would need the results directory to become unwritable during the run. The receipt states the code path.

## 8. Deployment assumptions

None of these involves a network connection to Azure or Kafka; plan rule 3's open-or-fresh connection and idle-or-load conditions do not apply, and no evidence was taken under load.

- D1: the xUnit reports have `<test>` elements with the `name` and `time` attributes of 3.4. Evidence: the pinned writer's source at 0c7aa4991fa340689155d52043ce88f52f2f7421 (lines in 3.4), read with curl in this planning session on 2026-11-13; the restored package `xunit.v3.runner.common` 4.0.2 in this sandbox's NuGet cache names that commit in its `.nuspec` `repository` element and carries the informational version `4.0.2+0c7aa4991f`; `Directory.Packages.props` line 7 pins `xunit.v3.mtp-v2` 4.0.2 and `tests/Parchwell.Kafka.Tests/packages.lock.json` lines 498 to 500 resolve `xunit.v3.runner.common` 4.0.2. Offline tests: `slowest`, `slowest_one` (stub reports in that format). Acceptance command 2 records a real report's entries.
- D2: the hosted `ubuntu-24.04` runner and local checkouts have `git` and a `.git` directory, so `source_sha` is a string there. Where not, the key is `null` (requirement 10, case `no_git`), and no exit status depends on it. Acceptance command 3 records the hosted value.

## 9. Acceptance commands

1. `./scripts/test-verify-dotnet.sh`: exits 0 and prints `verify-dotnet.sh checks passed.` Evidence: output in the receipt, with the case count.
2. `./scripts/verify-dotnet.sh --project Parchwell.Scenarios.Tests` in the implementing sandbox: stage lines carry UTC stamps; `jq -e . summary.json` succeeds; `jq '.outcome, .exit_code, (.slowest_tests | map_values(length))'` output recorded. Evidence: the stage lines, the `jq` output and the first three `slowest_tests` entries in the receipt.
3. Hosted `.NET verification` on the pull request's final head: passes; its log shows stage lines in the form of 3.2 and the full-gate line. Evidence: run link in the pull request description under docs/agents/pull-requests.md rule 8. The hosted run keeps no artifact of `summary.json`, because this plan changes no workflow.
4. `npm run check:docs`: no warning in a changed file, or each disposed of under docs/agents/pull-requests.md rule 8.

## 10. Sources

- `scripts/verify-dotnet.sh` on `main` at 2077964baaa918ccf7c671b5c2370511a10cc66f (identical to 675f837277824d197ce922511b9f55c7d77623b2 for every file in section 2): lines 7 to 20 (usage), 55 to 62 (tiers), 89 to 103 (SDK checks, results directory, exit trap), 111 to 133 (`run_logged`, `record_failure`), 137 to 170 (`run_tests`), 224 to 251 (fail-fast stages and project evaluation), 253 to 283 (test loop, timing table, final lines).
- `scripts/test-verify-dotnet.sh` at the same commit, lines 11 to 65 (stub and `run_case`) and 67 to 114 (cases).
- `docs/engineering/dotnet-verification.md` at the same commit, lines 48 to 74.
- `.github/workflows/dotnet-verification.yml` lines 15 to 27; `.github/CODEOWNERS`.
- xUnit `XmlV2ResultWriterMessageHandler.cs` lines 79 and 81 and `XmlUtility.cs` lines 22 to 63 at 0c7aa4991fa340689155d52043ce88f52f2f7421 (links in 3.4), fetched with curl 2026-11-13; package metadata in D1.
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on (plan rule 1 has no case here).

## 11. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of `<test>` elements per report is known only at runtime; it sizes one sort per report, and `slowest_tests` keeps at most 30 per report. The number of reports is bounded by the test projects (6 on `main`). Implementation effort has no runtime-sized step: the existing case list is fixed (9 cases at 2077964, `grep -c '^run_case '`) and the new cases are listed in section 7. No cap is needed.

(b) Stricter than a parent plan: there is no parent plan. Against the issue body and decision 6847884649, the plan adds: `summary.json` also on fail-fast exits; `source_dirty`; `outcome`; `failed_stages`; a fixed tie order for equal test times. Each records what the run did and changes no exit status. The issue's proposal 2 (probes and environment-limited mode) is not planned, by that decision.

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: the xUnit report format (D1), checked against the pinned writer's source and the restored package's metadata; `git` availability (D2), whose absence is a tested `null` path. Neither is an unsettled deployment assumption.

## 12. Open questions for Hari

None.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a literal expectation in section 7.

## 14. Changes since Revision 1

Revision 1: commit 44e4bcacd92d30d114e974e3cd997e49eebc49f4. This revision is design-changing (scope, contracts and requirements change), so it needs a new plan check under docs/agents/factory.md "Item lifecycle" step 1.

- Removed, by decision 6847884649 point 1: both environment probes (old 3.3), the exclusion and environment-limited mode and its listing stages (old 3.4), the `probes` and `excluded` keys, the `environment_limited_passed` outcome, `PARCHWELL_VERIFY_PROC_SELF`, the `timeout` and `docker` dependencies, the probe usage lines, the probe test cases and fixtures, and old deployment assumptions A1 to A5.
- Removed, by decision point 2: old open questions 1 and 2. The full-gate sentence at `docs/engineering/dotnet-verification.md` line 50 is left unchanged, so check finding 3 has no replacement text to pin. Check finding 4 (A4 load) falls away with A4.
- Applied from the check's `apply in implementation` P2s that survive the decision: P2-2 (own `TMPDIR` for exit-2 cases), P2-3 (`jq -c` comparison of slowest entry 0), P2-4 (`env -u` in `run_case`). P2-1 concerned the removed listing stage.
- Applied from the check's other P2s: section 2 now lists the tools the script uses on `main` correctly and names `git`, `date` and `nproc` as new; 3.4 states that `name` is xUnit's escaped display name and the `slow.xml` fixture uses a name the writer can produce; 3.5 lists the free text without the results directory path; `failed_stages` pins `Project evaluation` for the evaluation loop. The `Resolve` and unreadable-file findings concerned the removed Host probe.
- Added: pinned documentation wording (3.6), a numeric-order and `&amp;`-last boundary in `slow.xml`, cases `build_fail`, `eval_fail`, `help`, `slowest_one`, `cpu_text` and `no_git`, the `jq` prerequisite message, and the rule that writing `summary.json` never changes the exit status.
