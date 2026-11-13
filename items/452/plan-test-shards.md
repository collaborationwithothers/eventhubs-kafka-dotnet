# Plan: shard the slowest .NET test projects in CI (test-shards)

Issue: [#452](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452). This contract-grade plan covers the whole issue as one pull request, `test-shards`. It splits the Verifier tests into three CI jobs and the LoadGen tests into two, by test class, using a committed shard file. The aggregate `.NET verification` job gains a method-level check that every listed test method of every test project ran in exactly one job. Nothing is open for Hari.

Revision: r2. Planned against `main` at `9b13ce03d916bbd14e8d92445966fcfc09f34cfa`, which is still the head of `main` for r2.

Changes since r1, each from the r1 [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6856819660): section 4.0 states the restore and build time per job with its method (B1), adds the run completed after the r1 measurement (N4) and estimates the LoadGen `rest` shard (N2); R11 and R13 say "valid" instead of "parse" for `methods.json` (N3); section 11 (a) caps reruns for a test flake (N1); section 9 points to the stop rule in section 13, not section 12. No contract, requirement, test, scope or enforced check changes.

## 1. Goal and authority

Goal: shorten the critical path of the required `.NET verification` check, which after #441 is the slowest single test job, without dropping or repeating any test.

Authority, in creation order:

1. The issue body (created with the issue; read again for r2 on 2026-11-13; the issue's `updated_at` of 2026-11-13T15:04:47Z is the time of the newest comment). Hari applied `ready-for-agent` at 2026-11-13T14:39:24Z. The issue's comments are the r1 plan comment, its plan check and a handoff to the next planning session; none names a tool, so Claude Code implements (docs/agents/factory.md "Item lifecycle" step 2). Its proposal items 1 to 4 and its constraints are planned below: item 1 in section 4.0, item 2 in R1 to R6, item 3 in R7 to R16, item 4 in A6 and section 9.

No decision or approval comment exists on the issue (read for r2 at 2026-11-13, comments 6856499810, 6856819660 and 6856834074). No item state file exists (`items/452/` is absent on `main`). The issue body names no parent item and no split; the whole issue is the candidate (factory.md "Item lifecycle" step 3). Its prerequisite, the per-project matrix of #441, merged as pull request 454 at 2026-11-13T13:15:50Z. Context only, not authority on this issue: #441 decision [6851788370](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6851788370) sent sharding to this follow-up, "decided from measured per-job times after this change merges".

## 2. Scope

Files this pull request may change:

- `.github/workflows/dotnet-verification.yml`: in job `dotnet-tests`, only `name`, `strategy.matrix`, the `env` and `run` of step `Verify .NET test project`, the `run` of step `Collect verification reports`, and the artifact `name` of step `Upload verification reports`; in job `dotnet-verification`, only the `run` of step `Check every test project passed` (section 3.1).
- `scripts/dotnet-test-shards.tsv` (new): the shard file (section 3.2).
- `scripts/verify-dotnet.sh`: the `--shard` option, the test list stage and the `shard` summary key (R1 to R6).
- `scripts/test-verify-dotnet.sh`: the stub `dotnet run` case and the cases S1 to S12 (section 6.1).
- `scripts/check-dotnet-matrix.py`: R7 to R16.
- `scripts/tests/test_check_dotnet_matrix.py`: the fixture changes and tests of section 6.2.
- `infra/scripts/phase/tests/test_workflow.py`: only the `BASE_WORKFLOWS` value for `.github/workflows/dotnet-verification.yml` and the comment line above the dictionary (R17).
- `docs/engineering/dotnet-verification.md`: R18.
- `items/452/`: this plan and the authored receipt.

Must not change:

- The workflow's `name`, `on` block and `permissions` block (hard safety rule 8; `infra/scripts/lifecycle/check-offline.sh` line 87 checks the triggers); the `worker-images`, `lifecycle-verification` and `terraform-verification` jobs; the `dotnet-verification` job id, `name: .NET verification`, `needs`, `if`, `runs-on`, `timeout-minutes` and its other steps. The Kafka coverage steps keep `if: matrix.project == 'Parchwell.Kafka.Tests'`.
- `docs/operations/workflows/`: no committed copy of this workflow exists there (the folder holds `b10-abrupt-trial.yml.txt`, `cleanup-wip-branches.yml.txt`, `scenario.yml.txt` and `sweeper.yml.txt` on `main`), so the byte-for-byte part of hard safety rule 8 does not apply.
- Any test source, `.csproj`, `Directory.*.props`, lock file, `global.json`; `scripts/dotnet-verification.proj`, `scripts/verify-kafka-coverage.sh`, `scripts/verify-worker-images.sh`; `docs/operations/kafka-baseline.md`.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `results/`, `scenarios/`, any other workflow.

Non-goals: no test is moved, renamed, re-traited or re-collected; Kafka, Scenarios, Enrichment and ArchitectureTests are not sharded; the local full gate `./scripts/verify-dotnet.sh` with no options runs every project unfiltered, as today.

## 3. External contracts

### 3.1 Workflow

`dotnet-tests` job, exact text of the changed parts:

```yaml
  dotnet-tests:
    name: .NET tests (${{ matrix.unit }})
    runs-on: ubuntu-24.04
    strategy:
      fail-fast: false
      matrix:
        include:
          - project: Parchwell.ArchitectureTests
            shard: ''
            unit: Parchwell.ArchitectureTests
          - project: Parchwell.Enrichment.Tests
            shard: ''
            unit: Parchwell.Enrichment.Tests
          - project: Parchwell.Kafka.Tests
            shard: ''
            unit: Parchwell.Kafka.Tests
          - project: Parchwell.LoadGen.Tests
            shard: b10-end-to-end
            unit: Parchwell.LoadGen.Tests--b10-end-to-end
          - project: Parchwell.LoadGen.Tests
            shard: rest
            unit: Parchwell.LoadGen.Tests--rest
          - project: Parchwell.Scenarios.Tests
            shard: ''
            unit: Parchwell.Scenarios.Tests
          - project: Parchwell.Verifier.Tests
            shard: b10-verify-a
            unit: Parchwell.Verifier.Tests--b10-verify-a
          - project: Parchwell.Verifier.Tests
            shard: b10-verify-b
            unit: Parchwell.Verifier.Tests--b10-verify-b
          - project: Parchwell.Verifier.Tests
            shard: rest
            unit: Parchwell.Verifier.Tests--rest
```

Step `Verify .NET test project`:

```yaml
      - name: Verify .NET test project
        env:
          PARCHWELL_TEST_PROJECT: ${{ matrix.project }}
          PARCHWELL_TEST_SHARD: ${{ matrix.shard }}
        run: ./scripts/verify-dotnet.sh --project "$PARCHWELL_TEST_PROJECT" ${PARCHWELL_TEST_SHARD:+--shard "$PARCHWELL_TEST_SHARD"}
```

Step `Collect verification reports`: unchanged except that it also copies `$results_dir/$PARCHWELL_TEST_PROJECT/methods.json` to `$artifact/methods.json` when it exists, as it copies `results.xml`.

Step `Upload verification reports`: only `name` changes, to `dotnet-verification-${{ matrix.unit }}-attempt-${{ github.run_attempt }}`. For the unsharded projects this is the name on `main`.

Step `Check every test project passed` (aggregate job), `run` becomes:

```yaml
        run: python3 scripts/check-dotnet-matrix.py "$RUNNER_TEMP/dotnet-verification-reports" scripts/dotnet-test-shards.tsv "dotnet-tests=$DOTNET_TESTS_RESULT" "worker-images=$WORKER_IMAGES_RESULT"
```

A unit is the thing one matrix job verifies: a whole project (`<P>`) or one shard of it (`<P>--<shard>`). The unit string is the artifact name between `dotnet-verification-` and `-attempt-`.

### 3.2 Shard file `scripts/dotnet-test-shards.tsv`

Format, read identically by `verify-dotnet.sh` and the checker:

- A line that is empty, or whose first character is `#`, is ignored. Line numbers count every line from 1. A trailing `\r` is removed before parsing.
- Every other line has exactly three fields separated by single tab characters: `<project>`, `<shard>`, `<class>`.
- `<project>` matches `^Parchwell\.[A-Za-z0-9.]+$`. `<shard>` matches `^[a-z0-9]+(-[a-z0-9]+)*$` and is not `rest`. `<class>` starts with `<project>.` and contains no space or tab.
- A `<project>`, `<class>` pair appears at most once in the file, whatever the shard.

A project named on any line is sharded. Its shards are its named shards in order of first appearance, then `rest`. A named shard runs exactly the classes listed for it. `rest` runs every test of the project whose class is not listed for any of its named shards.

Contents, exactly (header comment text is the implementer's, one or more `#` lines):

```
Parchwell.LoadGen.Tests	b10-end-to-end	Parchwell.LoadGen.Tests.Abrupt.B10AbruptEndToEndTests
Parchwell.Verifier.Tests	b10-verify-a	Parchwell.Verifier.Tests.Component.B10AbruptVerifyPredicateBoundaryTests
Parchwell.Verifier.Tests	b10-verify-a	Parchwell.Verifier.Tests.Component.B10AbruptVerifyBoundaryTests
Parchwell.Verifier.Tests	b10-verify-a	Parchwell.Verifier.Tests.Component.B10AbruptVerifyTimeoutPrecedenceTests
Parchwell.Verifier.Tests	b10-verify-a	Parchwell.Verifier.Tests.Component.B10AbruptVerifyTerminalOutcomeTests
Parchwell.Verifier.Tests	b10-verify-a	Parchwell.Verifier.Tests.Component.B10AbruptVerifyBoundedReadTests
Parchwell.Verifier.Tests	b10-verify-b	Parchwell.Verifier.Tests.Component.B10AbruptVerifyCoverageTests
Parchwell.Verifier.Tests	b10-verify-b	Parchwell.Verifier.Tests.Component.B10AbruptVerifyTests
Parchwell.Verifier.Tests	b10-verify-b	Parchwell.Verifier.Tests.Component.B10AbruptVerifyClassificationTests
Parchwell.Verifier.Tests	b10-verify-b	Parchwell.Verifier.Tests.Component.B10ScientificThreeJobVerifyTests
```

These nine Verifier classes are every class in the xUnit collection `B10 abrupt verifier` on `main` (`[Collection("B10 abrupt verifier")]` in each of the nine files under `tests/Parchwell.Verifier.Tests/Component/`; definition at `B10AbruptVerifyBoundaryTests.cs` line 10, `DisableParallelization = true`). Each shard keeps whole classes, so each class fixture and the no-parallelization rule hold within a job.

### 3.3 `verify-dotnet.sh` command line

`./scripts/verify-dotnet.sh [--tier fast|all] [--project <name>]... [--shard <name>]`, plus `--shard=<name>`. Environment variable `PARCHWELL_TEST_SHARDS_FILE`: path of the shard file; when unset or empty, `<repository root>/scripts/dotnet-test-shards.tsv`.

Exact stderr messages, each followed by exit 2, before any `dotnet` call and before the results directory is created:

| Case | Message |
|---|---|
| `--shard` given with zero or more than one `--project` | `--shard needs exactly one --project.` |
| `--shard` given twice | `--shard may be given once.` |
| shard file unreadable | `Cannot read shard file <path>.` |
| shard file line breaks section 3.2 | `Malformed shard file line <n>: <path>.` |
| project not sharded | `<project> has no shards in <path>.` |
| shard not a shard of the project | `Unknown --shard '<name>' for <project>. Shards: <shards space-separated, in section 3.2 order>` |

`--shard` with no value prints the usage to stderr and exits 2, like `--tier`. The shard file is read and validated only when `--shard` is given.

Test arguments: with a named shard, the script passes `--filter-class` followed by that shard's classes in file order; with `rest`, `--filter-not-class` followed by every class listed for that project in file order. They go after `--`, after the tier arguments and before `--report-xunit-xml`.

New stage, for every test project run, with or without `--shard`: after the stage `<name> report` passes, the stage `<name> test list` runs `dotnet run --project <project path> --configuration Release --no-build --no-launch-profile -- -list methods/json -noColor`. Its standard output, and nothing else, is written to `<results directory>/<name>/methods.json`. A failing test list stage fails that project as a failing tests stage does today (recorded, the run continues).

`summary.json` gains the key `shard`, placed after `projects`: the `--shard` value as a string, or `null` without `--shard`. `schema_version` stays `1`.

### 3.4 Artifact files

As on `main`, plus `methods.json` (source `<results directory>/<project>/methods.json`), present only when its source exists.

### 3.5 Checker command line and findings

`python3 scripts/check-dotnet-matrix.py <reports directory> <shard file> <job>=<result> [<job>=<result>...]`

Usage line, exactly: `usage: check-dotnet-matrix.py <reports directory> <shard file> <job>=<result>...`. Standard library only. Exit codes, the success line and the finding line form are as on `main` (`check-dotnet-matrix: <code>: <detail>` on stderr). The success line still names projects, not units.

Finding codes, exactly: the ten on `main` (`job-result`, `no-artifacts`, `bad-artifact-name`, `missing-file`, `leg-not-passed`, `bad-summary`, `report-stage`, `inventory-mismatch`, `missing-project`, `extra-project`) plus `bad-shard-file`, `bad-methods`, `bad-results`, `unlisted-test`, `class-outside-shard`, `shard-class-not-run`, `methods-mismatch`, `method-in-two-shards`, `method-not-run`, `missing-shard`, `unexpected-artifact`. Detail formats are in R7 to R16.

## 4. Requirements

### 4.0 Measurement (issue item 1)

Taken 2026-11-13 by `gh api` from the five completed `.NET verification` runs after pull request 454 merged (four for r1, and run 43444471615, completed at 14:50:51Z, added for r2), and from the artifacts of run 43440889079, by script. Each job ran on a fresh GitHub-hosted `ubuntu-24.04` runner with 4 processors (`nproc` in its `summary.json`) and no other load from the run.

Job durations in seconds (`completed_at` minus `started_at`):

| Job | [43432170016](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43432170016) | [43432234380](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43432234380) | [43434696092](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43434696092) | [43440889079](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43440889079) | [43444471615](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43444471615) |
|---|---|---|---|---|---|
| Verifier | 782 | 805 | 764 | 796 | 796 |
| LoadGen | 779 | 777 | 723 | 777 | 741 |
| Kafka | 348 | 381 | 369 | 379 | 338 |
| Scenarios | 114 | 91 | 100 | 85 | 106 |
| ArchitectureTests | 95 | 112 | 107 | 107 | 79 |
| Enrichment | 79 | 89 | 129 | 88 | 108 |
| Worker image smoke | 69 | 58 | 47 | 57 | 61 |
| `.NET verification` (aggregate) | 9 | 8 | 9 | 9 | 9 |

The longest test job per run is 782, 805, 764, 796 and 796 s.

From run 43440889079's artifacts: Verifier tests stage 690 s (8412 tests), LoadGen 668 s (1828 tests), Kafka 257 s. Restore and build per job, the `elapsed_s` of the stages `Locked restore` plus `Solution build` in each `summary.json`: Verifier 70, LoadGen 71, Kafka 77, ArchitectureTests 72, Enrichment 59, Scenarios 53 s. Job time minus tests stage time: Verifier 106 s (796 - 690), LoadGen 109 s (777 - 668).

Where the time goes (xUnit report `time` attributes, run 43440889079):

- Verifier: collection `B10 abrupt verifier` (`DisableParallelization = true`) totals 492.5 s: PredicateBoundary 126.5, Coverage 113.7, Boundary 72.5, Verify 71.5, Classification 53.5, TimeoutPrecedence 20.7, TerminalOutcome 11.8, ScientificThreeJob 11.5, BoundedRead 10.7. Shard `b10-verify-a` sums 242.2 s and `b10-verify-b` 250.2 s. Slowest single Verifier test: 40.0 s.
- LoadGen: `B10AbruptEndToEndTests` is one test of 359.8 s in its own collection. Collection `B10AbruptEnvironment` (`DisableParallelization = true`) totals 264.4 s, mostly `B10AbruptProducerTests` 128.9 and `B10AbruptRealProcessTests` 126.5. The other 78 collections total 216.9 s; the longest of them is `ProbeV2ComponentTests` at 56.6 s.
- Kafka: `PhantomSpikeTests` is one test of 207.1 s.

Estimate (2026-11-13; basis: the class and collection sums above plus the measured 106 to 109 s between job time and tests stage time; not measured; the new test list stage of R4 adds time that is not measured and not included): the LoadGen `b10-end-to-end` shard is about 470 s (359.8 + 109). The LoadGen `rest` shard is about 370 to 590 s: its tests stage lies between the 264.4 s of its serial collection and the 481.3 s sum of all its test times (264.4 + 216.9), plus 109 s. The Verifier B10 shards are about 360 s each (242.2 or 250.2, plus 106), the Verifier `rest` shard about 300 s (690 - 492.5, plus 106). Kafka, unchanged, stays at 338 to 381 s. So the longest job is estimated at about 470 to 590 s, against 764 to 805 s now. A single test of 359.8 s bounds the LoadGen `b10-end-to-end` shard, so further sharding cannot go below it.

### 4.1 Script (`scripts/verify-dotnet.sh`)

- **R1 Shard selection.** WHEN `--shard <s>` is given with exactly one `--project <P>` and the shard file is valid and lists `<s>` for `<P>`, THEN the tests stage of `<P>` passes `--filter-class <classes of s in file order>`. WHEN `<s>` is `rest` and `<P>` is sharded, THEN it passes `--filter-not-class <every class listed for P in file order>`. Placement is as section 3.3. Without `--shard`, no class filter is passed.
- **R2 Rejections.** WHEN any case of the section 3.3 table holds, THEN the script prints that message to stderr and exits 2 before any `dotnet` call and without creating a results directory. Cases are checked in the table's order.
- **R3 Shard file validity.** The script rejects every rule of section 3.2 that a line can break, citing the first bad line. A file with only ignored lines is valid and shards nothing.
- **R4 Test list stage.** WHEN a project's report stage passes, THEN the stage `<name> test list` runs and writes `methods.json` as section 3.3 states. WHEN the tests or report stage fails, THEN the test list stage does not run. WHEN the test list command exits non-zero, THEN the project is recorded as failed with that status.
- **R5 Summary.** `summary.json` carries `shard` as section 3.3 states, for every exit after the results directory exists. Every other key keeps its meaning.
- **R6 Messages and usage.** The usage text names `--shard` and `PARCHWELL_TEST_SHARDS_FILE`. A run with `--shard` prints the partial-run line, as any `--project` run does today.

### 4.2 Checker (`scripts/check-dotnet-matrix.py`)

Terms: `<U>` is a unit string (section 3.1). `<P>` is its project. Ordinal order is Python's default string sort. A method string is `<type>.<method>` from a `<test>` element's `type` and `method` attributes, or an entry of `methods.json`. Its class is the text before its last `.`.

- **R7 Arguments.** WHEN fewer than three arguments are given, or an argument after the second is malformed as on `main`, THEN exit 2 with the usage line only.
- **R8 Shard file.** The checker reads the shard file under section 3.2. WHEN a line breaks a rule, or names a project not in the expected set of R14, THEN it reports `bad-shard-file: <shard file>: line <n>` for each such line in line order, and treats the file as sharding nothing. WHEN the file cannot be read, THEN it reports `bad-shard-file: <shard file>: unreadable` and treats it the same way.
- **R9 Artifact names.** An entry matches when it is a directory named `^dotnet-verification-(.+)-attempt-([1-9][0-9]*)$`, and group 1 either contains no `--` or splits at its first `--` into a nonempty project and a shard that matches the section 3.2 shard pattern or is `rest`. Otherwise `bad-artifact-name: <entry>`. The highest attempt per unit is checked, as on `main` per project.
- **R10 Per-unit files and summary.** For each checked unit, the `main` rules R7 to R9 of the #441 plan apply with `<U>` in place of `<P>` in every finding detail, with these changes: `methods.json` joins the required files, after `results.xml`; `bad-summary` checks `shard` after `projects`, which must equal the unit's shard as a string, or JSON `null` for an unsharded unit; `projects` must equal `["<P>"]`; `report-stage` also requires a stage labelled `<P> test list` with exit code `0`.
- **R11 Methods and results.** `methods.json` is valid when it parses as JSON and is an array of strings without duplicates. WHEN `methods.json` exists but is not valid, THEN `bad-methods: <U>`. WHEN `results.xml` exists but does not parse as XML, THEN `bad-results: <U>`. WHEN `methods.json` is valid and `results.xml` parses, THEN for each method string of a `<test>` element not in that unit's `methods.json`, in ordinal order without repeats, report `unlisted-test: <U>: <method>`.
- **R12 Class placement.** For a unit of a sharded project with parseable `results.xml`: WHEN its shard is named and a `<test>` type is not one of that shard's classes, THEN `class-outside-shard: <U>: <type>`; WHEN its shard is `rest` and a `<test>` type is a class listed for `<P>`, THEN the same finding. Each type once, ordinal order. WHEN a class listed for a named shard has no `<test>` element in that unit, THEN `shard-class-not-run: <U>: <class>`, in file order.
- **R13 Project completeness.** A project is complete-checkable when every expected unit of it (R14) has a checked artifact whose `methods.json` is valid under R11 and whose `results.xml` parses. This rule reads only the expected units' artifacts; unexpected units are left to R14. For each such project, in ordinal order: WHEN its expected units' `methods.json` sets differ, THEN `methods-mismatch: <P>` and nothing else from this rule for `<P>`. Otherwise, WHEN a method string occurs in `<test>` elements of more than one unit, THEN `method-in-two-shards: <P>: <method>`; WHEN a listed method occurs in no unit's `<test>` elements, THEN `method-not-run: <P>: <method>`. Each in ordinal order of method. A method that occurs many times inside one unit (theory rows) is not a finding.
- **R14 Expected units.** The expected projects are as on `main` (R11 of the #441 plan). An unsharded expected project expects unit `<P>`; a sharded one expects `<P>--<s>` for each of its shards. WHEN an expected unsharded unit has no artifact, THEN `missing-project: <P>`. WHEN an expected shard unit has no artifact, THEN `missing-shard: <U>`. WHEN an artifact's project is not expected, THEN `extra-project: <P>` once per project. WHEN an artifact's project is expected but its unit is not, THEN `unexpected-artifact: <U>`. Per-unit checks still run on unexpected and extra units.
- **R15 Inventory.** As on `main`, with units in place of projects; the reference inventory is the first unit in ordinal order that has `unit-projects.txt`.
- **R16 Output order.** `job-result` in argument order; `no-artifacts`; `bad-shard-file` in line order; `bad-artifact-name` in ordinal order; then per checked unit in ordinal order its findings in this order: `missing-file`, `leg-not-passed`, `bad-summary`, `report-stage`, `bad-methods`, `bad-results`, `unlisted-test`, `class-outside-shard`, `shard-class-not-run`, `inventory-mismatch`; then R13 findings per project in ordinal order; then `missing-project`, `missing-shard`, `extra-project`, `unexpected-artifact`, each in ordinal order. Every finding is reported; the checker never stops at the first.

### 4.3 Other files

- **R17 Workflow hash test.** The `BASE_WORKFLOWS` value for `.github/workflows/dotnet-verification.yml` in `infra/scripts/phase/tests/test_workflow.py` becomes the SHA-256 of the final workflow bytes. The comment line `# except the .NET verification logging update authorized in the PR407 receipt and the job split of issue #441.` becomes `# except the .NET verification logging update authorized in the PR407 receipt, the job split of issue #441 and the test shards of issue #452.` No other line in that file changes.
- **R18 Engineering doc.** `docs/engineering/dotnet-verification.md` states: the `--shard` option, the shard file and its format by reference to the file, and `PARCHWELL_TEST_SHARDS_FILE` ("Tiers, partial runs and failure reporting"); the `<name> test list` stage and `methods.json` (same section); the `shard` row of the `summary.json` table, after `projects`; in "Hosted CI and completion", one job per unit, the unit artifact name, and that the aggregate checker also requires each listed test method to run in exactly one job of its project and each sharded class to run only in its shard. It states no time saving except a measured value with its run link (A6). The sentence on the MTP filter combination cites the source in section 10.

## 5. Invariants and safety rules

- Every test that runs on `main` still runs exactly once per workflow run, and the aggregate checker proves it at method level (R13). Every other check of the #441 aggregate stays.
- A filter that selects too little fails closed: `method-not-run`. A filter that selects too much fails closed: `class-outside-shard` and `method-in-two-shards`. A stale class name fails closed: `shard-class-not-run`, and a shard with no tests at all fails `--minimum-expected-tests 1`.
- A shard file that disagrees with the matrix fails closed: `missing-shard` or `unexpected-artifact`.
- No Azure credential, no `id-token`, no trigger change, no permission change (hard safety rules 3, 8, 9). The required check name stays on the aggregate job.
- Artifacts hold test names, method names, timings and stage results only; no secret enters them (hard safety rule 7).

## 6. Test map

### 6.1 Script tests (`scripts/test-verify-dotnet.sh`)

The stub `dotnet` gains a `run` case: it logs `RUN <project name> <arguments>` to `$STUB_LOG` and prints `${STUB_METHODS:-[]}` to stdout, then exits `${STUB_LIST_STATUS:-0}`. Existing cases keep every assertion. `tested` still counts `TEST` lines only.

| Case | Requirement | Input | Expected |
|---|---|---|---|
| S1 | R1, R5 | `--project Parchwell.Verifier.Tests --shard b10-verify-a` | exit 0; exactly one `TEST` line, containing `-- --filter-class Parchwell.Verifier.Tests.Component.B10AbruptVerifyPredicateBoundaryTests Parchwell.Verifier.Tests.Component.B10AbruptVerifyBoundaryTests Parchwell.Verifier.Tests.Component.B10AbruptVerifyTimeoutPrecedenceTests Parchwell.Verifier.Tests.Component.B10AbruptVerifyTerminalOutcomeTests Parchwell.Verifier.Tests.Component.B10AbruptVerifyBoundedReadTests --report-xunit-xml` and no `--filter-not-class`; summary `.shard == "b10-verify-a"`, `.projects == ["Parchwell.Verifier.Tests"]`, `.outcome == "partial_passed"` |
| S2 | R1 | `--project Parchwell.Verifier.Tests --shard rest` | exit 0; `TEST` line contains `-- --filter-not-class ` followed by all nine Verifier classes of section 3.2 in file order, then ` --report-xunit-xml`; no `--filter-class ` |
| S3 | R1 | `--project=Parchwell.LoadGen.Tests --shard=b10-end-to-end` | exit 0; `TEST` line contains `-- --filter-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptEndToEndTests --report-xunit-xml` |
| S4 | R1 | `--tier fast --project Parchwell.LoadGen.Tests --shard rest` | `TEST` line contains `-- --filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2 --filter-not-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptEndToEndTests --report-xunit-xml` |
| S5 | R2 | `--shard rest` (no project); `--project Parchwell.Verifier.Tests --project Parchwell.LoadGen.Tests --shard rest` (subtests) | exit 2; output contains `--shard needs exactly one --project.`; empty stub log; empty `TMPDIR` |
| S6 | R2 | `--project Parchwell.Verifier.Tests --shard rest --shard rest` | exit 2; `--shard may be given once.`; no `dotnet` call; no results directory |
| S7 | R2 | `--project Parchwell.Verifier.Tests --shard nope` | exit 2; `Unknown --shard 'nope' for Parchwell.Verifier.Tests. Shards: b10-verify-a b10-verify-b rest`; no `dotnet` call; no results directory |
| S8 | R2 | `--project Parchwell.Scenarios.Tests --shard rest` | exit 2; `Parchwell.Scenarios.Tests has no shards in <repository root>/scripts/dotnet-test-shards.tsv.`; no `dotnet` call |
| S9 | R2, R3 | `PARCHWELL_TEST_SHARDS_FILE` pointing at fixtures, with `--project Parchwell.Verifier.Tests --shard rest`: missing file; a line with two fields; shard `rest`; shard `Bad`; class `Other.X`; the same project and class twice (second line is line 3, after a `#` line) (subtests) | exit 2; `Cannot read shard file <path>.` for the first; `Malformed shard file line <n>: <path>.` with the bad line's number for the others; no `dotnet` call |
| S10 | R4 | default run with `STUB_METHODS='["A.B.C"]'` | exit 0; six `RUN` lines, each containing `--configuration Release --no-build --no-launch-profile -- -list methods/json -noColor`; each project directory's `methods.json` equals `["A.B.C"]` plus newline exactly as the stub printed it; summary has a stage `<P> test list` with exit code 0 for each of the six projects, after `<P> report`; `.shard == null` |
| S11 | R4 | default run with `STUB_LIST_STATUS=3` | exit 3; `.failed_stages` lists all six projects with exit code 3, in run order; warning control still ran |
| S12 | R4 | `STUB_FAIL=Parchwell.Kafka.Tests` | no `RUN` line for `Parchwell.Kafka.Tests`; `RUN` lines for the other five |

### 6.2 Checker tests (`scripts/tests/test_check_dotnet_matrix.py`)

Fixture V (unsharded) is the existing fixture with these additions: each artifact has `methods.json` = `[]`; each summary has `"shard": null` after `projects` and a third stage `{"label":"<P> test list","elapsed_s":1,"start_utc":"2026-11-13T00:00:02Z","end_utc":"2026-11-13T00:00:03Z","exit_code":0}`; the checker gets a shard file holding only `# none`. Every existing test T1 to T24 runs against V with the shard file argument added; T4's expected usage line becomes the new one and gains the subtest "directory and shard file only" (exit 2).

Fixture W (sharded) is V with the `Parchwell.Verifier.Tests` artifact replaced by `dotnet-verification-Parchwell.Verifier.Tests--s1-attempt-1` and `dotnet-verification-Parchwell.Verifier.Tests--rest-attempt-1`, each with `summary.json` naming project `Parchwell.Verifier.Tests` and its shard; shard file `Parchwell.Verifier.Tests\ts1\tParchwell.Verifier.Tests.A`; both `methods.json` = `["Parchwell.Verifier.Tests.A.M1","Parchwell.Verifier.Tests.B.M2"]`; `s1` `results.xml` = `<assemblies><assembly><collection><test type="Parchwell.Verifier.Tests.A" method="M1" name="A.M1" /></collection></assembly></assemblies>`; `rest` the same with `B`, `M2`. V-based negative tests use the unit `<P>`; W-based ones use `Parchwell.Verifier.Tests--s1` (`S1`) and `Parchwell.Verifier.Tests--rest` (`REST`) below. Each negative test asserts exit 1 and the exact stderr lines listed, nothing more.

| Test | Requirement | Input | Expected stderr (after `check-dotnet-matrix: `) |
|---|---|---|---|
| T25 | R13, R14 | W | exit 0; stdout T1's success line |
| T26 | R14 | W without the `rest` artifact | `missing-shard: Parchwell.Verifier.Tests--rest` |
| T27 | R14 | W plus a valid unsharded `Parchwell.Verifier.Tests` artifact (methods `[]`, results `<assemblies />`) | `unexpected-artifact: Parchwell.Verifier.Tests` |
| T28 | R14 | V plus a valid `Parchwell.Scenarios.Tests--rest` artifact; W plus a valid `Parchwell.Verifier.Tests--s2` artifact with W's methods and empty results (subtests) | `unexpected-artifact: <that unit>` |
| T29 | R12, R13 | W, `REST` results also hold test `A`/`M1` | `class-outside-shard: Parchwell.Verifier.Tests--rest: Parchwell.Verifier.Tests.A`, then `method-in-two-shards: Parchwell.Verifier.Tests: Parchwell.Verifier.Tests.A.M1` |
| T30 | R12, R13 | W, `S1` results also hold test `B`/`M2` | `class-outside-shard: Parchwell.Verifier.Tests--s1: Parchwell.Verifier.Tests.B`, then `method-in-two-shards: Parchwell.Verifier.Tests: Parchwell.Verifier.Tests.B.M2` |
| T31 | R13 | W, both `methods.json` add `Parchwell.Verifier.Tests.B.M3` | `method-not-run: Parchwell.Verifier.Tests: Parchwell.Verifier.Tests.B.M3` |
| T32 | R13 | W, `REST` `methods.json` adds `Parchwell.Verifier.Tests.B.M3` | `methods-mismatch: Parchwell.Verifier.Tests` |
| T33 | R11 | W, `S1` results add test `A`/`M9` | `unlisted-test: Parchwell.Verifier.Tests--s1: Parchwell.Verifier.Tests.A.M9` |
| T34 | R12 | W, shard file adds `Parchwell.Verifier.Tests\ts1\tParchwell.Verifier.Tests.C` | `shard-class-not-run: Parchwell.Verifier.Tests--s1: Parchwell.Verifier.Tests.C` |
| T35 | R11, R13 | W, `S1` `methods.json` = `not json`; = `{}`; = `[1]`; = `["x","x"]` (subtests) | `bad-methods: Parchwell.Verifier.Tests--s1` |
| T36 | R11, R13 | W, `S1` `results.xml` = `not xml` | `bad-results: Parchwell.Verifier.Tests--s1` |
| T37 | R10 | W, `S1` summary `shard` = `"rest"`; = `null`; key removed; V, Scenarios summary `shard` = `"s1"` (subtests) | `bad-summary: <unit>: shard` |
| T38 | R10 | V, Kafka summary lacks the test list stage; its exit code is 1 (subtests) | `report-stage: Parchwell.Kafka.Tests` |
| T39 | R10 | V, Kafka `methods.json` removed; W, `REST` `methods.json` removed (subtests) | `missing-file: <unit>/methods.json` |
| T40 | R9 | V plus directories `dotnet-verification-Parchwell.Kafka.Tests--Bad-attempt-1`, `dotnet-verification-Parchwell.Kafka.Tests--rest--x-attempt-1`, `dotnet-verification---rest-attempt-1` (subtests) | `bad-artifact-name: <entry>` |
| T41 | R13 | V, Scenarios `methods.json` = `["Parchwell.Scenarios.Tests.X.M"]` | `method-not-run: Parchwell.Scenarios.Tests: Parchwell.Scenarios.Tests.X.M` |
| T42 | R13 | W, `S1` results hold test `A`/`M1` three times with different `name` values | exit 0 |
| T43 | R8 | V with shard file: one line of two fields; shard `rest`; shard `Bad`; class `Other.X`; the same project and class twice (second on line 2); project `Parchwell.Nope.Tests` (subtests) | `bad-shard-file: <path>: line <n>` only |
| T44 | R8 | V with a missing shard file path | `bad-shard-file: <path>: unreadable` |
| T45 | R16 | W with `worker-images=failure`, `S1` lacking `leg-passed`, `S1` results also holding `B`/`M2` | exactly, in order: `job-result: worker-images is 'failure', expected 'success'`, `leg-not-passed: Parchwell.Verifier.Tests--s1`, `class-outside-shard: Parchwell.Verifier.Tests--s1: Parchwell.Verifier.Tests.B`, `method-in-two-shards: Parchwell.Verifier.Tests: Parchwell.Verifier.Tests.B.M2` |

Standing fixture rule. When an existing test's fixture is rejected by a check this plan adds, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change. (Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.) Known cases: the V additions above for T1 to T24 under (a); T4's usage line under (b); the R17 hash under (a), whose assertion in `test_mirror_and_all_preexisting_workflows_unchanged` stays; `scripts/test-verify-dotnet.sh` cases whose `summary.json` or stub log checks see the new stage, `RUN` lines or `shard` key, under (a).

Workflow requirements of section 3.1 are checked by A3 to A6, not by unit tests: their behaviour is GitHub's, which the hosted run on the pull request head exercises.

## 7. Deployment assumptions

No Azure resource, container image, chart or live connection is involved. Each item depends on GitHub-hosted runner or tool behaviour; each is settled by recorded output or is checked by the pull request's own hosted run (A6) and fails closed.

| Assumption | Evidence, and how it was taken |
|---|---|
| A matrix with only `include` runs one job per entry | Run [42357132742](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42357132742) of `verifier-c5-mutation.yml`, whose `mutation` matrix has only `include` with 61 entries (lines 16 to 28 and on): the jobs API lists 62 jobs, the 61 legs plus `aggregate`. Fresh hosted runners, idle. |
| The MTP options `--filter-class` (several values after one switch, OR) and `--filter-not-class` (several values, each excluded), combined with another filter type as AND, behave as in section 3.3 | Local run in this planning session, 2026-11-13, SDK 10.0.402, fresh process per command, idle machine: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build -- --filter-class Parchwell.Verifier.Tests.A01.A01C3EvidenceRecorderTests Parchwell.Verifier.Tests.A01.WorkloadRecorderTests` ran tests of exactly those two classes (43 and 6); `-- --filter-namespace Parchwell.Verifier.Tests.A01 --filter-not-class <same two>` ran only `A01EvidenceRecorderTests` (66). Sources in section 10. R12 and R13 fail closed if the hosted runner differs. |
| `dotnet run ... -- -list methods/json -noColor` lists exactly the methods a full run executes | Local run in this planning session, same conditions, on `main` at `9b13ce0`: for each of the six test projects, the listing equalled, as a set and without duplicates, the `type.method` set of the `results.xml` from run 43440889079 (ArchitectureTests 17, Enrichment 21, Kafka 261, LoadGen 790, Scenarios 85, Verifier 1611 methods). Without `-noColor` the listing ends with an ANSI reset sequence, which breaks the JSON; with it the output parsed. The hosted run of A6 repeats this on the runner; any difference fails closed through R11 or R13. |
| Docker is available to every shard job | Today's Verifier and LoadGen jobs pass their `Tier=Docker` tests on the same runner label (run 43440889079). |

Re-runs and runner concurrency: the matrix grows from six to nine test jobs. GitHub's concurrent job limit for this account was not read; queueing would show in A6's measurement, and nothing here relies on the limit.

## 8. Acceptance commands

| # | Command | Evidence it must produce |
|---|---|---|
| A1 | `python3 -m unittest discover -s scripts/tests -p test_check_dotnet_matrix.py -v` | T1 to T45 run and pass; output in the receipt |
| A2 | `./scripts/test-verify-dotnet.sh` | prints `verify-dotnet.sh checks passed.`; output in the receipt |
| A3 | `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | passes, including `test_mirror_and_all_preexisting_workflows_unchanged` with the new hash |
| A4 | `bash infra/scripts/lifecycle/check-offline.sh`, with the pinned tools from `infra/scripts/lifecycle/install-tools.sh` on `PATH` | passes: trigger check, actionlint and shellcheck |
| A5 | a script comparing, between `main` and the head parsed with PyYAML: `on`, `permissions`, jobs `worker-images`, `lifecycle-verification`, `terraform-verification`, and every key of `dotnet-verification` except step `Check every test project passed`'s `run` | all equal; output in the receipt |
| A6 | the hosted `.NET verification` run on the pull request head | nine `.NET tests (<unit>)` jobs, `Worker image smoke` and `.NET verification` concluded `success`; nine artifacts `dotnet-verification-<unit>-attempt-<n>`, each with `methods.json`; the aggregate log shows A1's output and the T1-form success line naming six projects. The receipt records, by script from the jobs API, every job's duration and the longest `dotnet-tests` job, beside the section 4.0 table, with run URL and head SHA. |
| A7 | `npm run check:docs` | warnings fixed or explained under AGENTS.md "Before opening a pull request" rule 8 |
| A8 | `./scripts/verify-dotnet.sh --project Parchwell.LoadGen.Tests --shard b10-end-to-end` in a session with Docker and cgroup v2, if available | passes with one test in the report; when the session lacks cgroup v2 (docs/engineering/dotnet-verification.md line 70), record that A6 replaces it |

The pull request changes no .NET source or test, so the local full gate is not repeated (AGENTS.md "Before opening a pull request" rule 13); A6 runs every test on the hosted runners.

## 9. Measured saving (issue item 4)

The pull request description and the receipt state a time saving only as A6's measured job durations, beside section 4.0's measured baseline, with run links. The estimate in section 4.0 is not repeated as a result. WHEN A6's longest `dotnet-tests` job is not shorter than 764 s (the smallest longest job of the five baseline runs), THEN the implementer records the measurement and hands off to Hari instead of changing shard membership, which is a contract change (section 13).

## 10. Sources

Read 2026-11-13. Tools: WebFetch for xunit.net and docs.github.com; the xUnit in-process runner's own help (`dotnet tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll --help`, xUnit.net v3 In-Process Runner v4.0.2+0c7aa4991f); `dotnet run --help` on SDK 10.0.402; `gh api` for this repository's runs, jobs, artifacts and issue.

- xUnit, [Microsoft Testing Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform): `--filter-class "name"` "Run all tests in a given test class"; `--filter-not-class "name"` "Do not run any tests in the given test class"; "multiple filters of the same kind can be specified with just a single switch". The page does not state how filters combine.
- xUnit runner help, v4.0.2 (the pinned `xunit.v3.mtp-v2` 4.0.2): "If more than one simple filter type is specified, cross-filter type filters act as an AND operation"; `-class`: "if specified more than once, acts as an OR operation"; `-class-`: "if specified more than once, acts as an AND operation"; `-list <option>` with `methods - list class+method names of every method which is a test` and "you can add '/json' to the end of any option to get the listing in JSON format"; `-noColor`.
- xUnit, [Query filter language](https://xunit.net/docs/query-filter-language): "you may use either query filters or simple filters, but you may not use both at the same time." This plan uses simple filters only.
- `dotnet run --help`, SDK 10.0.402: `--no-launch-profile` "Do not attempt to use launchSettings.json or [app].run.json to configure the application."; `--no-build` "Do not build the project before running. Implies --no-restore."
- GitHub, [Running variations of jobs in a workflow](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/run-job-variations): "The value of `include` is a list of objects." It does not state the include-only case, which section 7 settles from a recorded run.
- The #441 plan's sources for upload, download, `needs` and required-check behaviour (`items/441/plan-dotnet-matrix.md` section 9 on `main`); this plan changes none of those steps' behaviour.
- Repository on `main` at `9b13ce0`: `.github/workflows/dotnet-verification.yml` lines 13 to 118; `scripts/verify-dotnet.sh` lines 7 to 63, 196 to 247, 306 to 339; `scripts/check-dotnet-matrix.py` lines 1 to 160; `scripts/tests/test_check_dotnet_matrix.py` lines 1 to 100; `scripts/test-verify-dotnet.sh` lines 17 to 56; `infra/scripts/phase/tests/test_workflow.py` lines 665 to 675 and 802 to 805; `infra/scripts/lifecycle/check-offline.sh` line 87; `.github/workflows/verifier-c5-mutation.yml` lines 14 to 28; `tests/Parchwell.Verifier.Tests/Component/B10AbruptVerifyBoundaryTests.cs` lines 10 to 16; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOptionsTests.cs` line 9; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs` lines 20 to 22; `docs/engineering/dotnet-verification.md` lines 48 to 97 and 155 to 169.

## 11. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of hosted-run failures on the pull request head that need a fix. Cap: three hosted runs of A6 that end in failure caused by this pull request's changes. Exhaustion action: push -wip and hand off to the next session. Reruns of an unchanged head for a test flake do not count toward that cap; they have their own cap of two reruns per pull request, with exhaustion action push -wip and hand off to the next session, which records the flaking test. A8 runs at most once. Nothing else scales: the shard file, the matrix, the test map and the doc edits are fixed.

(b) Stricter than the parent: there is no parent plan. Against the issue: proposal item 3 asks to keep the check that every test ran exactly once. The aggregate on `main` proves it per project only (every project has one passing artifact). This plan proves it per test method for every project, sharded or not (R11 to R13), and adds class placement (R12) and shard-to-matrix agreement (R14). Item 2 allows "another MTP filter"; this plan uses class filters only, so each class fixture and each `DisableParallelization` collection stays within one process per job.

(c) Dependencies on code, data or behaviour neither on `main` nor in the pull request: the xUnit filter and listing behaviour and the GitHub include-only matrix behaviour, each in section 7 with recorded output. The class names of section 3.2 are on `main` (section 10 lines). The timing data of section 4.0 comes from recorded runs and is used only for the choice of shards and the estimate.

## 12. Open questions for Hari

None.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3 (shard membership included), a requirement in section 4, or the scope in section 2.
