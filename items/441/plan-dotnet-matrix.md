# Plan: run the .NET verification per test project (dotnet-matrix)

Issue: [#441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441). This contract-grade plan covers the whole issue as one pull request, `dotnet-matrix`. It splits the `.NET verification` job into one job per test project, adds a separate worker image job, and keeps the required check name on a new aggregate job that fails unless every test project ran and passed with a validated report. One point is not decided here: whether this pull request also shards the Verifier test project. It is the only entry under "Open questions for Hari".

Revision: r1. Planned against `main` at `259a31fca2f53842385d820b5dba9f213d9e787e`.

## 1. Goal and authority

Goal: cut the wall time of the required .NET check without dropping any check it runs today. Today one job runs every test project in sequence, then the Kafka mapping coverage, then the worker image smoke (`.github/workflows/dotnet-verification.yml` lines 13 to 33). After this pull request those parts run in parallel jobs, and one aggregate job decides the required check.

Authority, in creation order:

1. The issue body (created 2026-11-13T07:05:58Z, read at `updated_at` 2026-11-13T08:28:22Z). Hari applied `ready-for-agent`; no comment names a tool, so Claude Code implements (docs/agents/factory.md "Item lifecycle" step 2). Its proposal items 1, 3 and 4 and its three constraints are planned below. Item 2 (Verifier shards) is the open question.
2. Comment [6847978940](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6847978940) (2026-11-13T08:28:21Z). It is not headed "## Decision", so it is evidence, not a decision. It records the ruleset read: one required check, context `.NET verification`, integration id 15368, strict policy.

No decision or approval comment exists on the issue. No item state file exists (`items/441/state.md` is absent on `main`). There is no parent item and no split; the whole issue is the candidate (factory.md "Item lifecycle" step 3).

## 2. Scope

Files this pull request may change:

- `.github/workflows/dotnet-verification.yml`: the `dotnet-verification` job is replaced by the three jobs in section 3.1. Nothing else in the file changes.
- `scripts/check-dotnet-matrix.py` (new): the aggregate checker.
- `scripts/tests/test_check_dotnet_matrix.py` (new): its tests.
- `infra/scripts/phase/tests/test_workflow.py`: in `B10AbruptWorkflowTests`, only the `BASE_WORKFLOWS` value for `.github/workflows/dotnet-verification.yml` and the comment line above the dictionary (R12).
- `docs/engineering/dotnet-verification.md`: "Tiers, partial runs and failure reporting" first paragraph, "Enforced gates" sentence on the CI mapping coverage steps, and "Hosted CI and completion" (R13).
- `docs/operations/kafka-baseline.md`: section "CI mapping coverage" (R14).
- `items/441/`: this plan and the authored receipt.

Must not change:

- The workflow's `name`, `on` block (hard safety rule 8; `infra/scripts/lifecycle/check-offline.sh` line 87 checks it), and `permissions` block.
- The `lifecycle-verification` and `terraform-verification` jobs, byte for byte.
- `scripts/verify-dotnet.sh`, `scripts/verify-kafka-coverage.sh`, `scripts/verify-worker-images.sh`, `scripts/dotnet-verification.proj`, `scripts/test-verify-dotnet.sh`.
- Any test source, `.csproj`, `Directory.*.props`, lock file, `global.json`.
- `docs/operations/workflows/`: no committed copy of this workflow exists there (the folder holds `b10-abrupt-trial.yml.txt`, `cleanup-wip-branches.yml.txt`, `scenario.yml.txt` and `sweeper.yml.txt` on `main`), so the byte-for-byte part of hard safety rule 8 does not apply. This confirms the issue's constraint.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `results/`, `scenarios/`, any other workflow.

## 3. External contracts

### 3.1 Workflow jobs

Exact text for the parts below; step order is as listed. Every other step attribute not shown is absent.

Job `dotnet-tests`:

```yaml
  dotnet-tests:
    name: .NET tests (${{ matrix.project }})
    runs-on: ubuntu-24.04
    strategy:
      fail-fast: false
      matrix:
        project:
          - Parchwell.ArchitectureTests
          - Parchwell.Enrichment.Tests
          - Parchwell.Kafka.Tests
          - Parchwell.LoadGen.Tests
          - Parchwell.Scenarios.Tests
          - Parchwell.Verifier.Tests
```

Steps of `dotnet-tests`, in order:

1. `Check out repository`: identical to the current step (checkout `3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`, same `ref` and `persist-credentials: false`).
2. `Set up pinned .NET SDK`: identical to the current step.
3. `Verify .NET test project`: `env: PARCHWELL_TEST_PROJECT: ${{ matrix.project }}`, `run: ./scripts/verify-dotnet.sh --project "$PARCHWELL_TEST_PROJECT"`.
4. `Install mapping coverage collector`: `if: matrix.project == 'Parchwell.Kafka.Tests'`; `run` identical to the current step.
5. `Verify Kafka mapping coverage`: `if: matrix.project == 'Parchwell.Kafka.Tests'`; `run` identical to the current step.
6. `Record passed test job`: no `if`; `run: mkdir -p "$RUNNER_TEMP/dotnet-verification-artifact" && touch "$RUNNER_TEMP/dotnet-verification-artifact/leg-passed"`.
7. `Collect verification reports`: `if: always()`; `env: PARCHWELL_TEST_PROJECT: ${{ matrix.project }}`; `shell: bash`; a `run` block that meets R4. Its text is the implementer's.
8. `Upload verification reports`:

```yaml
      - name: Upload verification reports
        if: always()
        uses: actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02 # v4.6.2
        with:
          name: dotnet-verification-${{ matrix.project }}-attempt-${{ github.run_attempt }}
          path: ${{ runner.temp }}/dotnet-verification-artifact
          if-no-files-found: error
```

Job `worker-images`: `name: Worker image smoke`, `runs-on: ubuntu-24.04`, steps `Check out repository` and `Set up pinned .NET SDK` identical to the current ones, then `Verify worker images start` with `run: ./scripts/verify-worker-images.sh`.

Job `dotnet-verification` (the required check):

```yaml
  dotnet-verification:
    name: .NET verification
    needs: [dotnet-tests, worker-images]
    if: always()
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    steps:
      - name: Check out repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          ref: ${{ github.event.pull_request.head.sha || github.sha }}
          persist-credentials: false
      - name: Test the aggregate checker
        run: python3 -m unittest discover -s scripts/tests -p test_check_dotnet_matrix.py -v
      - name: Download verification reports
        uses: actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093 # v4.3.0
        with:
          pattern: dotnet-verification-*
          path: ${{ runner.temp }}/dotnet-verification-reports
          merge-multiple: false
      - name: Check every test project passed
        if: always()
        env:
          DOTNET_TESTS_RESULT: ${{ needs.dotnet-tests.result }}
          WORKER_IMAGES_RESULT: ${{ needs.worker-images.result }}
        run: python3 scripts/check-dotnet-matrix.py "$RUNNER_TEMP/dotnet-verification-reports" "dotnet-tests=$DOTNET_TESTS_RESULT" "worker-images=$WORKER_IMAGES_RESULT"
```

The job id `dotnet-verification` and the name `.NET verification` are unchanged from `main`, so the required check context recorded in comment 6847978940 stays satisfied by this job. The matrix job names and `Worker image smoke` differ from it, as that comment requires.

### 3.2 Artifact layout

Artifact name: `dotnet-verification-<project>-attempt-<n>`, where `<project>` is the matrix value and `<n>` is `github.run_attempt`. Files at the artifact root, each present only when its source exists:

| File | Source in the job |
|---|---|
| `summary.json` | `<results directory>/summary.json` |
| `stage-timing.tsv` | `<results directory>/stage-timing.tsv` |
| `unit-projects.txt` | `<results directory>/unit-projects.txt` |
| `results.xml` | `<results directory>/<project>/results.xml` |
| `leg-passed` | empty file written by step 6 |

`<results directory>` is the one directory matching `${TMPDIR:-/tmp}/parchwell-dotnet-verification.*` (the `mktemp` template at `scripts/verify-dotnet.sh` line 261).

### 3.3 Checker command line

`python3 scripts/check-dotnet-matrix.py <reports directory> <job>=<result> [<job>=<result>...]`

Standard library only. Exit 0 when no finding; 1 when at least one finding; 2 for a usage error. Every finding is one line on stderr, `check-dotnet-matrix: <code>: <detail>`. Usage errors print `usage: check-dotnet-matrix.py <reports directory> <job>=<result>...` on stderr. On success it prints one line on stdout:

`check-dotnet-matrix: <count> test projects passed with validated reports: <names joined by ", ">`

Finding codes, exactly: `job-result`, `no-artifacts`, `bad-artifact-name`, `missing-file`, `leg-not-passed`, `bad-summary`, `report-stage`, `inventory-mismatch`, `missing-project`, `extra-project`. Detail formats are in R5 to R11.

## 4. Requirements

Terms: an artifact directory is an entry of `<reports directory>`. `<P>` is a project name. Ordinal order is Python's default string sort.

- **R1 Matrix runs each project.** WHEN the workflow runs, THEN one `dotnet-tests` job runs per matrix value, each calling `./scripts/verify-dotnet.sh --project <value>`, and a failing job does not cancel the others (`fail-fast: false`).
- **R2 Kafka coverage stays.** WHEN the matrix value is `Parchwell.Kafka.Tests`, THEN steps 4 and 5 run after step 3, as today's steps run after the full verification. WHEN it is any other value, THEN they are skipped.
- **R3 Passed marker.** WHEN steps 1 to 5 all succeeded (or were skipped by their `if`), THEN step 6 writes `leg-passed`. WHEN any earlier step failed, THEN `leg-passed` is absent (default `success()` condition).
- **R4 Collection.** WHEN step 7 runs, THEN it creates `$RUNNER_TEMP/dotnet-verification-artifact` if absent and copies each source in section 3.2 that exists into it under the listed name. WHEN the number of directories matching `${TMPDIR:-/tmp}/parchwell-dotnet-verification.*` is not exactly 1, THEN it copies nothing from them, prints `Expected one verification results directory; found <n>.` to stderr and exits 1. Step 8 still uploads whatever the directory holds.
- **R5 Job results.** WHEN an argument `<job>=<result>` has `<result>` other than `success`, THEN the checker reports `job-result: <job> is '<result>', expected 'success'` (an empty result prints `''`). WHEN fewer than two arguments are given, an argument after the first has no `=`, an empty job name, or a job name repeated, THEN exit 2 with the usage line and no other output.
- **R6 Artifact discovery.** WHEN `<reports directory>` does not exist or has no entries, THEN report `no-artifacts: <reports directory>`. WHEN an entry is not a directory or its name does not match `^dotnet-verification-(.+)-attempt-([1-9][0-9]*)$`, THEN report `bad-artifact-name: <entry name>`. WHEN several directories name the same `<P>`, THEN only the one with the highest attempt number is checked; the others are ignored without a finding.
- **R7 Required files.** For each checked artifact of `<P>`, in this order: WHEN `summary.json`, `stage-timing.tsv`, `unit-projects.txt` or `results.xml` is missing, THEN report `missing-file: <P>/<file>` for each. WHEN `leg-passed` is missing, THEN report `leg-not-passed: <P>`.
- **R8 Summary.** WHEN `summary.json` exists but is not valid JSON or not a JSON object, THEN report `bad-summary: <P>: not a JSON object` and skip R9 for `<P>`. Otherwise, WHEN the first failing key in this order fails, THEN report `bad-summary: <P>: <key>` once: `schema_version` must equal `1`; `tier` must equal `"all"`; `projects` must equal `["<P>"]`; `exit_code` must equal `0`; `outcome` must equal `"partial_passed"`; `failed_stages` must equal `[]`. A missing key fails as that key. Booleans never equal numbers (`true` is not `1`).
- **R9 Validated report.** WHEN `stages` is not a list, or it has no object with `label` `"<P> tests"` and `exit_code` `0`, or no object with `label` `"<P> report"` and `exit_code` `0`, THEN report `report-stage: <P>`. This rejects an empty scaffold, which the script passes with no report stage (`scripts/verify-dotnet.sh` lines 434 to 438).
- **R10 Inventory agreement.** The inventory of an artifact is the set of names from `unit-projects.txt`: each non-blank line, trailing whitespace removed, cut after its last `/`, with a trailing `.csproj` removed. The reference inventory is that of the first artifact, in ordinal order of `<P>`, that has `unit-projects.txt`. WHEN another artifact's inventory differs from the reference as a set, THEN report `inventory-mismatch: <P>`. Directory prefixes do not matter.
- **R11 Coverage of the inventory.** The expected set is `Parchwell.ArchitectureTests` plus the reference inventory (empty when no artifact has `unit-projects.txt`), as `scripts/verify-dotnet.sh` lines 424 to 433 run them. WHEN an expected name has no artifact, THEN report `missing-project: <name>`. WHEN an artifact's `<P>` is not expected, THEN report `extra-project: <P>`; its other checks still run.
- **R11a Output order.** Findings print in this order: `job-result` in argument order; `no-artifacts`; `bad-artifact-name` in ordinal entry order; then for each checked `<P>` in ordinal order, its R7, R8, R9 and R10 findings in that order; then `missing-project` in ordinal order; then `extra-project` in ordinal order. All findings are reported; the checker never stops at the first.
- **R11b Success.** WHEN no finding exists, THEN print the success line with the expected set in ordinal order and exit 0.
- **R12 Workflow hash test.** The `BASE_WORKFLOWS` value for `.github/workflows/dotnet-verification.yml` in `infra/scripts/phase/tests/test_workflow.py` becomes the SHA-256 of the final workflow bytes. The comment line `# except the .NET verification logging update authorized in the PR407 receipt.` becomes `# except the .NET verification logging update authorized in the PR407 receipt and the job split of issue #441.` No other line in that file changes. Precedent: commit `49df522` changed the same value and comment for an authorized workflow change.
- **R13 Engineering doc.** `docs/engineering/dotnet-verification.md` states: hosted CI runs `./scripts/verify-dotnet.sh --project <name>` in one job per test project, and the Kafka coverage steps in the Kafka test project's job; the worker image smoke runs in its own job; the `.NET verification` check is the aggregate job, which runs `scripts/check-dotnet-matrix.py` and fails unless both other jobs succeeded and every project of the scaffold inventory plus `Parchwell.ArchitectureTests` has a passing summary, a passed report stage and the passed marker; each project job uploads the artifact of section 3.2; the local full gate is still `./scripts/verify-dotnet.sh` with no options, and a single matrix job is a partial run. It replaces the sentence "Hosted CI runs exactly that." and rewrites the first paragraph of "Hosted CI and completion" to match; it adds the download-artifact pin with a link to the v4.3.0 tag. It keeps the existing pins and the paragraphs on issue #47.
- **R14 Kafka baseline doc.** `docs/operations/kafka-baseline.md` "CI mapping coverage" states that the coverage steps run in the Kafka test project's matrix job after `./scripts/verify-dotnet.sh --project Parchwell.Kafka.Tests`, and its YAML block shows both steps with the `if: matrix.project == 'Parchwell.Kafka.Tests'` line.

## 5. Invariants and safety rules

- Every check the current job runs still runs on every pull request and push to `main`: restore, build, Kafka XML inventory, project evaluation, scaffold inventory, every non-empty test project with report validation, the warning control (in every matrix job), the Kafka spike evidence (Kafka job, tier `all`), the Kafka mapping coverage, the worker image smoke.
- The required check passes only through the aggregate job's checker. A skipped aggregate job reports "Success" to required checks (GitHub, "Troubleshooting required status checks"), so the aggregate job runs with `if: always()`, which that page recommends for required checks that use `needs`.
- No Azure credential, no `id-token`, no new trigger, no permission change (hard safety rules 3, 8, 9).
- `needs.<job>.result` is documented for a job, but GitHub's pages read for this plan do not state how a matrix job's result combines its legs. The checker therefore does not rely on `dotnet-tests=success` alone: R7 to R11 check every project's artifact as well. Either check failing fails the aggregate.
- Artifacts hold test names, timings and stage results only; no secret enters them (hard safety rule 7). Hidden files are excluded by upload-artifact v4.4 and later, and none is written.

## 6. Test map

All tests are in `scripts/tests/test_check_dotnet_matrix.py`, Python `unittest`, building fixtures in a temporary directory and running the checker as a subprocess with `sys.executable`.

Valid fixture V: six directories `dotnet-verification-<P>-attempt-1` for `P` in `Parchwell.ArchitectureTests`, `Parchwell.Enrichment.Tests`, `Parchwell.Kafka.Tests`, `Parchwell.LoadGen.Tests`, `Parchwell.Scenarios.Tests`, `Parchwell.Verifier.Tests`. Each holds `summary.json` = `{"schema_version":1,"tier":"all","projects":["<P>"],"exit_code":0,"outcome":"partial_passed","failed_stages":[],"stages":[{"label":"<P> tests","elapsed_s":1,"start_utc":"2026-11-13T00:00:00Z","end_utc":"2026-11-13T00:00:01Z","exit_code":0},{"label":"<P> report","elapsed_s":1,"start_utc":"2026-11-13T00:00:01Z","end_utc":"2026-11-13T00:00:02Z","exit_code":0}]}`, `stage-timing.tsv` = one line `<P> tests\t1\t2026-11-13T00:00:00Z\t2026-11-13T00:00:01Z`, `results.xml` = `<assemblies />`, an empty `leg-passed`, and `unit-projects.txt` = five lines `/w/tests/Parchwell.<X>.Tests/Parchwell.<X>.Tests.csproj` for `X` in `Enrichment`, `Kafka`, `LoadGen`, `Scenarios`, `Verifier`. Job arguments: `dotnet-tests=success worker-images=success`. Each negative test changes one thing in V, so no other finding can mask it; each asserts the exit status and the exact stderr.

| Test | Requirement | Input (change to V) | Expected |
|---|---|---|---|
| T1 | R11b | V | exit 0; stdout `check-dotnet-matrix: 6 test projects passed with validated reports: Parchwell.ArchitectureTests, Parchwell.Enrichment.Tests, Parchwell.Kafka.Tests, Parchwell.LoadGen.Tests, Parchwell.Scenarios.Tests, Parchwell.Verifier.Tests`; stderr empty |
| T2 | R5 | `worker-images=` with each of `failure`, `cancelled`, `skipped`, empty (subtests) | exit 1; stderr exactly `check-dotnet-matrix: job-result: worker-images is '<value>', expected 'success'` |
| T3 | R5 | `dotnet-tests=failure`, `worker-images=success` | exit 1; `check-dotnet-matrix: job-result: dotnet-tests is 'failure', expected 'success'` |
| T4 | R5 | argument lists: only the directory; `worker-images` with no `=`; `=success`; `dotnet-tests=success dotnet-tests=success` (subtests) | exit 2; stderr exactly the usage line |
| T5 | R6 | reports directory absent; reports directory empty (subtests) | exit 1; `check-dotnet-matrix: no-artifacts: <path>` then `check-dotnet-matrix: missing-project: Parchwell.ArchitectureTests` |
| T6 | R6 | add a file `notes.txt`; add a directory `dotnet-verification-Parchwell.Kafka.Tests-attempt-0`; add a directory `other` (subtests) | exit 1; `check-dotnet-matrix: bad-artifact-name: <entry>` only |
| T7 | R6 | add `dotnet-verification-Parchwell.Kafka.Tests-attempt-2` valid, and make attempt-1 lack `leg-passed` | exit 0, T1's stdout |
| T8 | R6 | add `dotnet-verification-Parchwell.Kafka.Tests-attempt-2` lacking `leg-passed`, attempt-1 valid | exit 1; `check-dotnet-matrix: leg-not-passed: Parchwell.Kafka.Tests` only |
| T9 | R6 | attempt-10 lacking `leg-passed` beside valid attempt-9 for Kafka (numeric, not text, order) | exit 1; `leg-not-passed: Parchwell.Kafka.Tests` only |
| T10 | R7 | remove, in `Parchwell.LoadGen.Tests`, each of `summary.json`, `stage-timing.tsv`, `results.xml` (subtests) | exit 1; `check-dotnet-matrix: missing-file: Parchwell.LoadGen.Tests/<file>` only |
| T11 | R7, R10 | remove `unit-projects.txt` from `Parchwell.Verifier.Tests` | exit 1; `missing-file: Parchwell.Verifier.Tests/unit-projects.txt` only |
| T12 | R7, R3 | remove `leg-passed` from `Parchwell.Kafka.Tests` | exit 1; `leg-not-passed: Parchwell.Kafka.Tests` only |
| T13 | R8 | in `Parchwell.Scenarios.Tests`: `schema_version` 2; `schema_version` `true`; `tier` `"fast"`; `projects` `[]`; `projects` `["Parchwell.Scenarios.Tests","Parchwell.Kafka.Tests"]`; `projects` null; `exit_code` 1; `exit_code` `false`; `outcome` `"full_gate_passed"`; `outcome` `"failed"`; `failed_stages` `[{"label":"Warning control","exit_code":1}]`; key `outcome` removed (subtests) | exit 1; `check-dotnet-matrix: bad-summary: Parchwell.Scenarios.Tests: <key>` only, `<key>` the changed key |
| T14 | R8 | `tier` `"fast"` and `exit_code` 1 together | exit 1; `bad-summary: Parchwell.Scenarios.Tests: tier` only (first key in order) |
| T15 | R8 | `summary.json` = `not json`; = `[]` (subtests) | exit 1; `bad-summary: Parchwell.Scenarios.Tests: not a JSON object` only |
| T16 | R9 | in `Parchwell.Enrichment.Tests` stages: report stage removed; report `exit_code` 1; tests stage removed; `stages` = `[]` (empty scaffold run); `stages` key removed (subtests) | exit 1; `check-dotnet-matrix: report-stage: Parchwell.Enrichment.Tests` only |
| T17 | R9 | stage labelled `Parchwell.Kafka.Tests report` inside the `Parchwell.Enrichment.Tests` summary instead of its own | exit 1; `report-stage: Parchwell.Enrichment.Tests` only |
| T18 | R10 | `Parchwell.Verifier.Tests` inventory lacks the Enrichment line | exit 1; `check-dotnet-matrix: inventory-mismatch: Parchwell.Verifier.Tests` only |
| T19 | R10 | `Parchwell.Verifier.Tests` inventory uses prefix `/other/root/` and CRLF line ends | exit 0, T1's stdout |
| T20 | R11 | remove the `Parchwell.Verifier.Tests` directory | exit 1; `check-dotnet-matrix: missing-project: Parchwell.Verifier.Tests` only |
| T21 | R11 | remove `Parchwell.ArchitectureTests` directory | exit 1; `missing-project: Parchwell.ArchitectureTests` only |
| T22 | R11 | add a valid `dotnet-verification-Parchwell.Extra.Tests-attempt-1` (its summary names itself; same inventory) | exit 1; `check-dotnet-matrix: extra-project: Parchwell.Extra.Tests` only |
| T23 | R11, R10 | every `unit-projects.txt` adds `/w/tests/Parchwell.New.Tests/Parchwell.New.Tests.csproj` | exit 1; `missing-project: Parchwell.New.Tests` only |
| T24 | R11a | `worker-images=failure`, Verifier directory removed, `Parchwell.Kafka.Tests` lacks `leg-passed` | exit 1; stderr exactly the three lines `check-dotnet-matrix: job-result: worker-images is 'failure', expected 'success'`, `check-dotnet-matrix: leg-not-passed: Parchwell.Kafka.Tests`, `check-dotnet-matrix: missing-project: Parchwell.Verifier.Tests`, in that order |

Workflow requirements R1 to R4 and R12 to R14 are checked by acceptance commands A2 to A6, not by unit tests: their behaviour is GitHub's, which the hosted run on the pull request head exercises.

Standing fixture rule. When an existing test's fixture is rejected by a check this plan adds, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change. (Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.) The one known case is R12: the hash assertion in `test_mirror_and_all_preexisting_workflows_unchanged` takes the new file's hash under (a), and its assertion stays.

## 7. Deployment assumptions

Each item below depends on GitHub-hosted runner behaviour that no offline test covers. Each is settled by recorded output of this repository's own runs, so none is open. Each recorded job ran on a fresh GitHub-hosted runner, with no other load applied by the run; no item depends on load.

| Assumption | Recorded evidence |
|---|---|
| A matrix job can upload artifacts with `actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02` (v4.6.2) under workflow permission `contents: read`, and a later `needs` job with `if: always()` can download them with `actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093` (v4.3.0), `pattern` and `merge-multiple: false` | Run [42357132742](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42357132742) of `verifier-c5-mutation.yml` (permissions `contents: read`, lines 6 to 7): 123 artifacts listed by the artifacts API on 2026-11-13; job `aggregate` (128378403183) concluded `success`. The workflow file is unchanged on `main` since that run's head `17f42aed`. |
| On `ubuntu-24.04`, `verify-dotnet.sh` creates its results directory under `/tmp` | Job 131495744456 of run 43307845372 printed `Verification logs and reports: /tmp/parchwell-dotnet-verification.I2pG2KKR`. R4 uses `${TMPDIR:-/tmp}`, the script's own expression, so a set `TMPDIR` does not break it. |
| `python3` 3.12 is on `ubuntu-24.04` | Job 131495745033 (Lifecycle verification, run 43307845372) concluded `success`; its first step asserts `sys.version_info[:2] == (3, 12)` (workflow line 52). The checker uses the standard library only. |
| Docker is available to the Kafka job and the worker image job | Job 131495744456 passed `Parchwell.Kafka.Tests` (which includes the Docker characterization) and `verify-worker-images.sh`, which exits 1 when Docker is unreachable. |
| A job with `needs` and `if: always()` reports a check under its `name` | Run 42357132742 job `aggregate` is listed under its name by the jobs API. The required context `.NET verification` is satisfied today by a job of that name (comment 6847978940). The pull request's own hosted run (A5) shows the check on the head commit. |

Re-runs: whether artifacts of earlier attempts are visible to a later attempt's download is not verified. R6 gives a correct result either way: the highest attempt of each project is checked, and a project whose job was not re-run keeps its earlier passing artifact.

## 8. Acceptance commands

| # | Command | Evidence it must produce |
|---|---|---|
| A1 | `python3 -m unittest discover -s scripts/tests -p test_check_dotnet_matrix.py -v` | T1 to T24 run and pass; output in the receipt |
| A2 | `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` (the lifecycle job's command, without the `command.py` wrapper) | passes, including `test_mirror_and_all_preexisting_workflows_unchanged` with the new hash |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh`, with the pinned tools from `infra/scripts/lifecycle/install-tools.sh` on `PATH` | passes: trigger check, actionlint and shellcheck on the changed workflow |
| A4 | a script comparing `on`, `permissions`, `jobs.lifecycle-verification` and `jobs.terraform-verification` of the workflow on `main` and on the head, parsed with PyYAML | all four equal; output in the receipt |
| A5 | the hosted `.NET verification` workflow run on the pull request head | six `.NET tests (<project>)` jobs, `Worker image smoke` and `.NET verification` concluded `success`; the aggregate log shows A1's test output and the T1-form success line naming six projects; six artifacts named `dotnet-verification-<P>-attempt-<n>`; the run URL and head SHA in the pull request description |
| A6 | `npm run check:docs` | warnings fixed or explained under AGENTS.md "Before opening a pull request" rule 8 |

The pull request changes no .NET source or test, so the local full gate `./scripts/verify-dotnet.sh` is not repeated for it (AGENTS.md "Before opening a pull request" rule 13); A5 runs every project on the hosted runners.

## 9. Sources

Read 2026-11-13. Tools: WebFetch for docs.github.com; `git ls-remote` and `curl` for the action repositories at the pinned commits; `gh api` for this repository's runs, jobs, artifacts and comments.

- GitHub, [Troubleshooting required status checks](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/troubleshooting-required-status-checks): "A job is skipped by a conditional": "The job reports "Success""; "Use `always()` with `needs` for required checks that depend on other jobs."; "Successful check statuses are `success`, `skipped`, and `neutral`."
- GitHub, [Workflow syntax](https://docs.github.com/en/actions/writing-workflows/workflow-syntax-for-github-actions): `fail-fast` "defaults to `true`" and cancels in-progress and queued matrix jobs when one fails; "If a job fails or is skipped, all jobs that need it are skipped unless the jobs use a conditional expression that causes the job to continue."; "A matrix will generate a maximum of 256 jobs per workflow run."
- GitHub, [Contexts](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts): `needs.<job_id>.result` "Possible values are `success`, `failure`, `cancelled`, or `skipped`." `github.run_attempt` "begins at 1 for the workflow run's first attempt, and increments with each re-run." `runner.temp` "is emptied at the beginning and end of each job." No sentence on matrix jobs in `needs` was found.
- GitHub, [Expressions](https://docs.github.com/en/actions/reference/workflows-and-actions/expressions): `success()` "Returns `true` when all previous steps have succeeded."; "A default status check of `success()` is applied unless you include one of these functions."; `always()` "Causes the step to always execute, and returns `true`, even when canceled", with the warning to avoid it for getting sources. The aggregate job bounds that risk with `timeout-minutes: 10`, as the `aggregate` job of `verifier-c5-mutation.yml` does.
- `actions/upload-artifact` README at [`ea165f8d65b6e75b540449e92b4886f43607fa02`](https://github.com/actions/upload-artifact/blob/ea165f8d65b6e75b540449e92b4886f43607fa02/README.md) (tag v4.6.2 and v4): names must be unique, artifacts are immutable, hidden files excluded from v4.4, `if-no-files-found: error`, 90-day default retention.
- `actions/download-artifact` [action.yml](https://github.com/actions/download-artifact/blob/d3f86a106a0bac45b974a628896c90dbdf5c8093/action.yml) at `d3f86a106a0bac45b974a628896c90dbdf5c8093` (tag v4.3.0 and v4): `pattern`; `merge-multiple: false` extracts "into individual named directories within the specified path"; without `github-token` it downloads from "the current repository and the current workflow run".
- Repository on `main` at `259a31f`: `.github/workflows/dotnet-verification.yml` lines 1 to 33; `scripts/verify-dotnet.sh` lines 9 to 19, 73 to 88, 261, 419 to 455; `scripts/dotnet-verification.proj` lines 86 to 105 (inventory lines are full project paths; names not ending `.Tests` are skipped, so `Parchwell.ArchitectureTests` is never in it); `infra/scripts/phase/tests/test_workflow.py` lines 664 to 674 and 802 to 805; `infra/scripts/lifecycle/check-offline.sh` line 87; `.github/workflows/verifier-c5-mutation.yml` lines 6 to 7 and 158 to 192.
- Job log of job 131495744456 (run 43307845372): stage lines 188 to 6983 and step headers 6984 to 7030.

Estimate (2026-11-13, computed by script from job 131495744456's timestamps, not measured): the longest matrix job, Verifier, about 770 s (97.6 s from job start to the end of the scaffold inventory, 668 s Verifier tests and report, 4.4 s warning control), against 1740 s for today's job. LoadGen about 759 s, Kafka with coverage about 367 s, worker image job about 45 s. Excludes runner queueing and the aggregate job. The run uses eight runners at once instead of three; GitHub's concurrency limit for this account was not read, and nothing in this plan relies on it.

## 10. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of hosted-run failures on the pull request head that need a workflow fix. Cap: three hosted runs of A5 that end in failure caused by this pull request's changes. Exhaustion action: push -wip and hand off to the next session. Test-flake reruns of an unchanged head do not count. No other step scales: the matrix, the test map and the doc edits are fixed.

(b) Stricter than the parent: there is no parent plan. Against the issue: the checker also requires the passed marker (R3, R7), which brings the Kafka coverage and every later step under the required check; it requires a passed report stage, so an empty scaffold cannot pass (R9); and the worker image smoke stays under the required check as a separate job. Less strict than possible: the checker does not compare `source_sha` or `source_dirty` in `summary.json`, because no hosted run has yet recorded those values; artifacts come from the same run and the same checkout `ref`.

(c) Dependencies on code, data or behaviour neither on `main` nor in the pull request: only GitHub-hosted runner and action behaviour, each listed in section 7 with recorded output that settles it. The `summary.json` keys come from `scripts/verify-dotnet.sh` on `main` (lines 196 to 247) and are documented in `docs/engineering/dotnet-verification.md` lines 74 to 91.

## 11. Open questions for Hari

1. **Verifier shards (issue proposal item 2).** The issue proposes splitting the Verifier test project into shards and says balance and shared state are not checked yet. This plan does not decide it. Facts for the decision, from the issue's measured stage times: Verifier tests took 666 s and LoadGen tests 655 s, so sharding Verifier alone shortens the longest test stage by at most 11 s, because LoadGen becomes the longest. Sharding also needs a filter option passed through `scripts/verify-dotnet.sh` and recorded in `summary.json`, and a checker rule that proves the shards together cover every test (for example an include shard per namespace plus one shard excluding all of them). Options: (a) leave shards out of this pull request and track them in a follow-up issue; (b) include Verifier shards in this pull request, which needs a new plan revision that pins the shard list and the filter contract; (c) shard both Verifier and LoadGen, which goes beyond the issue text. Source: issue body, "Proposal" item 2 and the stage table.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2.
