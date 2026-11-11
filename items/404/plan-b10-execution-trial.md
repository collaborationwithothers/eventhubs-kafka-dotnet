# B10 restart execution part 2 (trial orchestration) contract-grade plan

Revision 1. This plan makes one B10 diagnostic trial runnable end to end on the merged part 1 code. The phase script runs the start line, the C1 timeout Job, the first restart Job and the successor Job in that order, and collects all of their evidence into the diagnostic artifact. A new test proves that the timeout Job's ordinary evidence cannot be admitted as a scientific result. The operations documents are rewritten to the protocol that `main` now implements. No workflow file changes, no verifier file changes, and Hari merges the pull request.

## 1. Goal and authority

Goal: deliver part 2 of the execution split, as decision 6775474492 leaves it after part 1 merged in pull request 430 (`3a4ee0b1`).

Authority, in creation order. Catch-up on 2026-11-11: 241 comments on issue 404, listed by script across three pages. The state file on `main` (`items/404/state.md`) covers comments up to 6777482103. Every decision and approval comment listed there, and every comment created after it, was read in full. Parent items 347 and 348 have no comment created or edited after the state file's read time.

1. The issue body and every decision listed in `items/404/state.md` "Decisions in force", in their order there. These are not restated here.
2. [Decision 6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939): deployment assumptions. The offline end-to-end test owns DA1, DA5 and the acceptance-to-kill time before the trial. DA2 to DA4 are accepted by name for the one trial only.
3. [Decision 6773282644](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6773282644): the execution split. Part 2 needs its own plan, check and approval.
4. [Decision 6774480762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774480762): option A1. The C1 timeout Job runs the ordinary host unchanged as `{run}-c1-r1-loadgen`. Point 3 requires the test of section 4, R6 to R8.
5. [Decision 6775474492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775474492): part 2 keeps the trial workflow change with its committed copy, the C1 timeout Job wiring, the phase orchestration of three Jobs, the `docs/operations/` rewrite and point 3's test.
6. [Approval 6776159640](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776159640) of part 1, `items/404/plan-b10-execution-loadgen.md` at `fc1a4f36452b2d302f1d73987e1ba5eb967b3d65`, with [amendment 6776685705](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776685705). Its section 3 is the interface this plan builds on. Its section 3.12 lists what part 2 must do.
7. [Decision 6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492): Hari's answers to this plan's shape questions. Q1 A: the verifier is unchanged, and the start line keeps two names. Hari accepts by name the gap against ADR-021 "Consequences" until the verifier classification pull request. Q2 A: no workflow change unless planning finds one is needed, and Hari merges. Q3 A: a phase-layer test. Defaults: Codex implements; the order of section 3.1; the timeout-Job outcome rule of section 3.2; collection with no ordinary verify; the four documents of section 3.6.
8. [PENDING Q4 and Q5, section 13. This item is replaced by the decision that answers them before the plan is pushed.]

ADR-021 in `docs/blueprint.md` on `main` governs. Where this plan and ADR-021 differ, ADR-021 wins and the stop rule applies, except for the start-line gap accepted in item 7.

Also read: [issue 428](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/428), the operations documents that still describe the stop protocol.

Not authority: the held offline end-to-end test plan and the paused classification plan (section 2.3 lists only the files they share).

## 2. Scope

### 2.1 Files allowed to change

1. `infra/scripts/phase/b10_abrupt.py`: `FAILURE_NAMES` (one added name, section 3.3), `prepare` (the run-ID prefix only, section 3.4), `run` (the timeout Job, section 3.1), and `collect` (the timeout Job's collection, section 3.5). Private helpers may be added.
2. `infra/scripts/phase/tests/test_b10_abrupt.py`: new tests of section 8, and the existing tests that section 8.3 lists, adapted to the new order and prefix. No assertion is removed or loosened.
3. `infra/scripts/phase/tests/check_b10_timeout_evidence.py`: new. It is the point 3 checker of section 3.7.
4. `charts/parchwell/tests/test_b10_abrupt_host.py`: one added export-and-check step after the existing wire bridge, and its final line (section 3.7).
5. `tests/Parchwell.LoadGen.Tests/Host/B10TimeoutEvidenceExportTests.cs`: new, the export of section 3.7. It may reuse code moved out of `tests/Parchwell.LoadGen.Tests/Host/B10SecondWorkerTests.cs`. If it does, that file may change only by moving its private fixture types to a shared internal location, with no change to any test method or assertion.
6. `docs/operations/b10-abrupt-contract.md`, `docs/operations/b10-workflow.md`, `docs/operations/loadgen-contract.md`, `docs/operations/verifier-contract.md`, at the places section 3.6 names.
7. `items/404/state.md`, under docs/agents/factory.md "Item state file", and a new receipt `items/404/execution-trial-receipt.md`. The implementing tool is Codex (decision 6787849492, point 4). The branch therefore starts with `codex/` and includes the issue number, and every commit carries the Codex trailer (AGENTS.md "Implementer and reviewer").

No `*.csproj`, `Directory.*.props`, `packages*.lock.json`, solution file, new project, assembly reference or NuGet package. No file in scope has an owner in `.github/CODEOWNERS`. `docs/operations/` is agent-owned (docs/agents/factory.md "Ruleset"). Hari merges anyway, by decision 6787849492 point 2. The implementing session stops at a ready pull request with Codex's code and security reviews on its head.

### 2.2 Paths that must not change

- `.github/**`, including `.github/workflows/b10-abrupt-trial.yml`, and `docs/operations/workflows/b10-abrupt-trial.yml.txt`. Section 6 shows that the trial needs no workflow change. If implementation finds one is needed, the stop rule applies (decision 6787849492, point 2).
- `src/**`, including every `Parchwell.Verifier*/` path and every ordinary-host and abrupt-host file under `src/Parchwell.LoadGen/`.
- `charts/parchwell/values.yaml`, `charts/parchwell/values.schema.json`, `charts/parchwell/templates/**`, and every chart test other than `test_b10_abrupt_host.py`.
- `infra/scripts/phase/phase.py`, including `B10_MODES` and `reject_b10_abrupt_science`. Point 3's test proves the existing guard and changes none of it.
- In `infra/scripts/phase/b10_abrupt.py`: `metadata` (the start-line check at line 313 keeps requiring `[{run}-c1-r1-loadgen, {run}-c1-r1-loadgen2]`, decision 6787849492 point 1), `accept_diagnostic_publication`, `release_successor`, `observe_first_termination`, `read_journal`, `envelope`, `validate_data` and the constants other than `FAILURE_NAMES`.
- `tests/Parchwell.Verifier.Tests/**`, `tests/Parchwell.ArchitectureTests/**`, `tests/Parchwell.LoadGen.Tests/Abrupt/**`, `infra/scripts/phase/tests/check_b10_abrupt_producers.py`.
- `scripts/**`, `scenarios/**` (no `expected.yaml`), `results/**`, `README.md`, `AGENTS.md`, `CLAUDE.md`, `docs/` outside `docs/operations/`, and `docs/operations/` outside the four files of section 2.1 item 6.

### 2.3 Files shared with other open plans on issue 404

| Other plan | Shared file | Effect on this plan |
|---|---|---|
| Offline end-to-end test (held) | none | Stop rule 3 only if it lands first and changes a file in section 2.1. |
| Classification (paused) | `items/404/state.md`; `docs/operations/verifier-contract.md` | It must carry the three-name start line (decision 6787849492, point 1). This plan records that in section 3.6. Stop rule 3 if it changes either file on `main` first. |

## 3. External contracts

Every literal here is pinned. "Policy" marks a value fixed by Hari's decisions, not a measurement.

### 3.1 Trial order (ADR-021 protocol rules 1 and 10; decision 6787849492, point 4)

`phase.sh run-b10-abrupt-trial` runs, under the one 18000-second orchestration deadline that `b10_deadline` already sets:

1. The start-line Job, `{run}-c1-r1-start-line`, and its metadata check, both unchanged.
2. The C1 timeout Job: chart mode `loadgen`, Job `{run}-c1-r1-loadgen`. It is rendered by `Phase.render('loadgen', 1)` from the ordinary `Phase.values`. The render hook does not run for it (`phase.py` line 660 selects only the two abrupt modes), so it receives no `b10_abrupt_diagnostic` value, no `B10Abrupt__` setting and no binding configmap. It is applied and waited for exactly as the trial's other Jobs are. It is not collected during `run`, so that no log collection delays the successor's release (the existing order test asserts no `collect-job` call during `run`).
3. The first restart Job, `{run}-c1-r1-loadgen-restart`, and everything after it, unchanged from part 1: first-pod termination, first publication, `accept_diagnostic_publication` (which requires `successor_permitted` true), and the successor Job `{run}-c1-r1-loadgen-successor` only through `release_successor`.

At most one producer Job exists at a time. Each Job is waited for before the next is applied.

### 3.2 Timeout-Job outcome rule (decision 6787849492, point 4; [PENDING Q5])

The trial branches on the timeout Job record's `outcome`:

| `outcome` | Action | `phase-status.json` | Return |
|---|---|---|---|
| `complete` | go on to the restart Job | record kept | per later Jobs |
| `failed` | go on to the restart Job | record kept | per later Jobs |
| `unknown` | no further Job; `safety_stop` true | `b10_abrupt_gaps` gains `b10_abrupt_timeout_job_unknown`, boundary `timeout_job` | 1 |
| `apply_failed` | as `unknown` | as `unknown` | 1 |
| `render_failed` | no further Job; `safety_stop` unchanged | `b10_abrupt_gaps` gains `b10_abrupt_configuration_invalid`, boundary `timeout_job` | 1 |

A timeout Job that ends `failed` never fails or repairs the restart chain. ADR-021 protocol rule 12 checks each instance on its own.

### 3.3 Failure names

`FAILURE_NAMES` gains exactly `b10_abrupt_timeout_job_unknown` [PENDING Q5]. No other name is added, removed or renamed. Every name in part 1's section 3.9 keeps its meaning.

### 3.4 Trial run ID [PENDING Q4]

The trial's run ID is `b10d-{GITHUB_RUN_ID}-{GITHUB_RUN_ATTEMPT}`. `Trial.prepare` passes prefix `b10d` to `prepare_shared`, in place of `b10`. B10 science keeps `b10` (`phase.py` line 414). Every Job name, configmap name and Blob prefix of the trial derives from the run ID, as before. The existing 29-character and DNS-label check in `prepare_shared` (line 459) is unchanged.

### 3.5 Diagnostic artifact

`collect-b10-abrupt-trial` collects, for each state record whose mode is `loadgen`, `loadgen-restart` or `loadgen-successor` and whose `apply` is not null, the Job's logs with the existing `collect-job` command, and copies `artifacts/<job>/` to `artifacts/b10-abrupt-diagnostic/<job>/`. The workflow uploads only `artifacts/b10-abrupt-diagnostic` (unchanged). For the timeout Job, collection runs no successor-receipt check, no ordinary verify and no evidence check beyond the recorded outcome. Its collection failures are recorded as the restart and successor Jobs' failures already are: `b10_abrupt_publication_ambiguous`, or `b10_abrupt_deadline_exhausted` for exit 124, at boundary `job_collection`.

The start-line record (mode `start-line`) is not copied. That is unchanged from `main`.

### 3.6 Operations documents (issue 428; decision 6787849492, point 4)

Each page states these facts and cites ADR-021 and the part 1 plan sections that own them. No page restates a scientific rule.

- `docs/operations/b10-abrupt-contract.md`: replace the whole superseded-protocol text, including its status line 5, its PR1, PR2 and PR3 paragraph (line 7), and the dangling link at line 174, with the protocol on `main`. It names: the three processes, flags and exit codes (part 1 section 3.1); the configuration keys and fixed policy values (3.2); the three Job names (3.3); the schema-3 envelope (3.4); the journal record kinds (3.5); the probe sequence (3.6); the parent (3.7); the termination classes and `successor_permitted` (3.8); the failure names, with `b10_abrupt_timeout_job_unknown` (3.9 and this plan's 3.3); the first publication (3.10); the successor (3.11); and this plan's sections 3.1, 3.2, 3.4 and 3.5.
- `docs/operations/b10-workflow.md`: replace lines 59 and 61 ("Diagnostic trial (inactive for science)") with this plan's sections 3.1, 3.2 and 3.5. It states that the start line keeps the two names `{run}-c1-r1-loadgen` and `{run}-c1-r1-loadgen2`, that the second name is a Job the trial never runs, and that the three-name start line belongs to the verifier classification pull request, as decision 6787849492 point 1 accepts. Line 5 keeps its meaning.
- `docs/operations/loadgen-contract.md`: line 65 names `--b10-abrupt-parent`, `--b10-abrupt-child`, `--b10-abrupt-successor` and `--validate-b10-abrupt-configuration`, in place of `--b10-abrupt-supervisor`. It states that the C1 timeout Job is an ordinary `loadgen` Job that publishes under `producer/`, and that point 3's test proves no scientific path admits it. Lines 81 and 102 say that the restart of ADR-021's redesign is in the B10 abrupt diagnostic contract, in place of naming the legacy section as that restart.
- `docs/operations/verifier-contract.md`: line 5 names `schema_version 3` in place of `schema_version 2`. It says the verifier support ADR-021 requires, including the start line's three C1 Job names, is planned for the verifier classification pull request, in place of "its third pull request".

### 3.7 Timeout-evidence export and checker (decision 6774480762 point 3; decision 6787849492 point 3)

Export. Test class `Parchwell.LoadGen.Tests.Host.B10TimeoutEvidenceExportTests` runs the ordinary LoadGen host through `Program.RunAsync`, as `B10SecondWorkerTests` runs its first worker. It uses scenario `B10`, configuration and phase `C1`, run `run-1`, Job `run-1-c1-r1-loadgen`, pod `pod-first`, an in-memory store and a synthetic producer. The bytes are synthetic workload output of production host code. The test asserts host exit 0 and the presence of every object listed below, whether or not the export variable is set. It is never skipped. With environment variable `PARCHWELL_B10_TIMEOUT_EVIDENCE_OUTPUT` set to a directory, it also writes every object the store holds under `run-1/C1/r1/producer/pod-first/`, at its path relative to `run-1/C1/r1/`, plus `export.json` (`{"files":[{name,length,sha256}]}`, names sorted ordinally, lowercase hex).

The export holds at least `producer/pod-first/registration.json`, `producer/pod-first/completion.json`, `producer/pod-first/publication/manifest.json`, `producer/pod-first/publication/ledger.jsonl` and `producer/pod-first/publication/index.json`. The checker requires the listing to equal the directory's files and each descriptor to match its bytes.

Checker. `python3 infra/scripts/phase/tests/check_b10_timeout_evidence.py <directory>` runs the section 8.2 cases on the exported bytes. It exits 0 and prints exactly `b10_timeout_evidence_rejected` and a newline on success. It exits 1 with a reason on stderr otherwise, including when the directory or `export.json` is missing.

Bridge. `b10_abrupt_host_acceptance` in `charts/parchwell/tests/test_b10_abrupt_host.py` runs the export class with the variable set, then the checker. It asserts exit 0 for both and the checker's exact output. Its final line becomes exactly `b10 abrupt host checks passed: 2 rendered environments, 7 rejected inputs, production-byte bridge, timeout-evidence rejection`. The required step "Verify the load generator accepts the rendered chart environment" (`.github/workflows/dotnet-verification.yml` line 87) runs it through `charts/parchwell/tests/check-loadgen-host.sh` and `test_loadgen_host.py` line 264, both unchanged.

## 4. Requirements

R1. WHEN `run-b10-abrupt-trial` runs and the start line completes, THEN the timeout Job is applied before the restart Job, and the restart Job is applied only after the timeout Job's wait returns (section 3.1).
R2. WHEN the timeout Job's outcome is `complete` or `failed`, THEN the trial continues exactly as on `main` from the restart Job, and the record stays in `phase-status.json` (section 3.2).
R3. WHEN the timeout Job's outcome is `unknown` or `apply_failed`, THEN no further Job is rendered or applied, `safety_stop` is true, the gap is `b10_abrupt_timeout_job_unknown` at boundary `timeout_job`, and `run` returns 1.
R4. WHEN the timeout Job's outcome is `render_failed`, THEN no further Job is rendered or applied, `safety_stop` is unchanged, the gap is `b10_abrupt_configuration_invalid` at boundary `timeout_job`, and `run` returns 1.
R5. WHEN `collect-b10-abrupt-trial` runs after an applied timeout Job, THEN its logs are collected and copied to `b10-abrupt-diagnostic/{run}-c1-r1-loadgen/`, with the failure mapping of section 3.5, and no successor check runs for it.
R6. WHEN a frozen trial `phase.json` (purpose `diagnostic_trial`, `b10_abrupt_diagnostic` true) exists, with no diagnostic environment variable set, and any scientific phase entry receives the timeout Job's exported bytes or runs, THEN it raises `Failure('b10_abrupt_diagnostic_not_scientific')` before any command, removes `candidate.json` and `candidate.json.partial`, and writes `full_allowed=false`.
R7. WHEN the same bytes reach `check_candidate` under an ordinary B10 C1 `phase.json` with no diagnostic marker and no diagnostic environment, THEN it raises `Failure('candidate contract invalid')` and not the diagnostic failure. This shows the bytes carry no marker, so R6's rejection comes from the trial state.
R8. WHEN a scientific B10 prepare and a trial prepare run with any `GITHUB_RUN_ID` and `GITHUB_RUN_ATTEMPT`, THEN their run IDs differ: the scientific one starts `b10-` and the trial one starts `b10d-` [PENDING Q4]. WHEN the scientific prepare runs with the trial's diagnostic environment, THEN it raises the diagnostic failure and writes no `phase.json`.
R9. WHEN the documents of section 3.6 are checked, THEN each states the facts listed there, and `npm run check:docs` reports no new warning in them.

## 5. Invariants and safety rules

- No workflow, verifier, chart template, chart value or LoadGen production file changes. The trial stays `workflow_dispatch` only, on `main` only (AGENTS.md hard safety rules 8 and 9).
- No `results/`, `expected.yaml` or verdict change (GOVERNANCE "Verdict integrity").
- No trial runs and no workflow is dispatched by this pull request or its implementation. Hari dispatches the trial after merge (ADR-021 protocol rule 15).
- The timeout Job's ordinary evidence is written by the unchanged ordinary host under `producer/`. No scientific path reads it: R6 to R8.
- One producer Job at a time (R1). An unknown producer state stops further producers (R3).

## 6. Workflow need check (decision 6787849492, point 2)

The trial needs no workflow change, for these reasons, each read on `main` at `3a4ee0b1`:
- All Job ordering is inside `phase.sh run-b10-abrupt-trial` and `collect-b10-abrupt-trial` (`b10-abrupt-trial.yml`, steps `trial` and `collect`).
- The job's `timeout-minutes: 360` exceeds the 18000-second orchestration deadline (`phase.py` line 28), which already bounds every trial Job.
- The upload path is already `b10-abrupt-diagnostic`, the only collection target of section 3.5.
- Cleanup (`delete-workload`, `destroy-ephemeral`) selects Jobs and configmaps by the run's instance label, which covers the timeout Job (`phase.py` line 1476, `delete_workload`).
- No workflow environment variable names the Job set.

## 7. Never-ran inventory

These stages have never run on Azure: the timeout Job inside the trial, the three-Job order, and the collection of section 3.5. Every stage of part 1's never-ran inventory (part 1 plan section 6) also remains never-ran. The one diagnostic trial after merge is their first run.

## 8. Test map

Inputs are synthetic. Expected values are literal.

### 8.1 Phase tests (`infra/scripts/phase/tests/test_b10_abrupt.py`, run by the required "Verify lifecycle with stub commands" step through `infra/scripts/tests/test_phase_bridge.py`)

| Test | Requirement | Inputs | Expected |
|---|---|---|---|
| T-O1 `test_trial_runs_timeout_job_before_restart` | R1, R2 | produced-bytes trial, every command stubbed as in `test_trial_order_and_delayed_logs_outside_release_path` | apply order `['loadgen', 'loadgen-restart', 'loadgen-successor']`; event order `['metadata', 'loadgen', 'loadgen-restart', 'termination', 'publication', 'loadgen-successor']`; state modes `['loadgen', 'loadgen-restart', 'loadgen-successor']`; no `collect-job` call during `run`; return 0. This is that existing test, adapted. It lives in `ProducedBytesCases` and runs through the bridge. |
| T-O2 `test_timeout_job_outcome_rule` | R2, R3, R4 | wait exit 0, 1, 124 and 2; apply exit 1; render returning None | 0 and 1: restart applied. 124, 2 and apply 1: outcome `unknown`, `unknown`, `apply_failed`; no restart render or apply; `safety_stop` true; gaps `['b10_abrupt_timeout_job_unknown']`; boundary `timeout_job`; return 1. Render None: no apply of any producer Job; `safety_stop` false; gaps `['b10_abrupt_configuration_invalid']`; return 1. |
| T-O3 `test_timeout_job_render_is_ordinary` | section 3.1 item 2 | trial phase with stubbed helm | the render hook is not called for `loadgen`; the values written for `{run}-c1-r1-loadgen` have no `b10_abrupt_diagnostic` and no `b10_abrupt_binding_configmap`. |
| T-C1 `test_collection_copies_timeout_job_into_diagnostic_artifact` | R5 | state modes `start-line`, `loadgen`, `loadgen-restart` (all applied), `collect-job` exit 0, an artifact tree per Job | `collect-job` called for `{run}-c1-r1-loadgen` and `{run}-c1-r1-loadgen-restart` only; `b10-abrupt-diagnostic/{run}-c1-r1-loadgen/` exists with the copied file; no start-line copy. |
| T-C2 `test_collection_command_and_copy_failures_record_named_gaps` (existing, extended) | R5 | the existing cases repeated with mode `loadgen` | exit 1: `b10_abrupt_publication_ambiguous`; exit 124: `b10_abrupt_deadline_exhausted`; copy OSError: `b10_abrupt_publication_failed`; boundary `job_collection`. |
| T-P1 `test_trial_and_science_run_ids_never_collide` [PENDING Q4] | R8 | `GITHUB_RUN_ID` in `1`, `42925777839`, and 22 nines; attempt `1` | trial run ID `b10d-<id>-1`; science B10 C1 run ID `b10-<id>-1`; never equal. Trial with 22 nines: accepted (29 characters). Trial with 23 nines: `invalid run or source commit`. |
| T-P2 `test_prepare_requires_explicit_full_selector_and_confirmation` (existing, adapted) | R8 | as on `main` | `prepare_shared` called once with `('B10', 'C1', 'C1', 'b10d', purpose='diagnostic_trial', run_kind='smoke', b10_abrupt_diagnostic=True)`. |
| T-F1 `test_timeout_failure_name_is_closed` | 3.3 | `FAILURE_NAMES` | contains `b10_abrupt_timeout_job_unknown`; set size is the `main` size plus 1 (computed by the test, not written as a literal). |

### 8.2 Point 3 cases on produced bytes (`check_b10_timeout_evidence.py`, run by the required chart-host step)

| Case | Requirement | Inputs | Expected |
|---|---|---|---|
| T-S1 frozen trial state rejects | R6 | trial `phase.json`; `PARCHWELL_PURPOSE`, `PARCHWELL_B10_ABRUPT_DIAGNOSTIC` and every `B10Abrupt__` variable unset; each exported file | for each file, `check_candidate(<bytes>)`; and once each, `prepare('C1')`, `validate_scenario()`, `run_repeats()`, `judge()`, `candidate()`, `candidate_download()`: `Failure` with message `b10_abrupt_diagnostic_not_scientific`; `Phase.run` not called; both candidate files absent afterwards; `full_allowed=false` in `GITHUB_OUTPUT`. |
| T-S2 the bytes are ordinary | R7 | ordinary B10 C1 `phase.json`, same unset environment, each exported JSON or JSONL file | no exported document has `dialect`, `purpose`, `scientific_admissible` or `b10_abrupt_diagnostic` at any depth; `check_candidate(<bytes>)` raises `Failure('candidate contract invalid')`. |
| T-S3 export integrity | 3.7 | the export directory | listing equals files; descriptors match; the five required names are present. A missing directory or `export.json` exits 1. |

### 8.3 Existing tests that change

Adapted to the order of section 3.1, and kept otherwise: `test_unfinished_first_job_names_its_site`, `test_produced_report_and_native_mutations_rejected`, `test_failed_publication_blocks_successor_in_real_trial`, `test_trial_records_approved_names_for_produced_transport_failures`, `test_unsuccessful_successor_records_named_trial_failure`, `test_trial_order_and_delayed_logs_outside_release_path` (becomes T-O1), and `test_prepare_requires_explicit_full_selector_and_confirmation` (T-P2). Each keeps every assertion it has on `main`. The only allowed edits are the added timeout Job in the expected order and in stubs, and the `b10d` prefix.

`test_every_scientific_entry_rejects_before_work_and_removes_candidate` stays unchanged. T-S1 adds the produced bytes and the unset environment.

### 8.4 Properties the cold reviewer checks by reading the diff

- `git diff --stat origin/main` lists only section 2.1 paths.
- `.github/workflows/b10-abrupt-trial.yml` and its committed copy are byte-identical to `main`.
- No `src/` file changes.

## 9. Acceptance commands

Each command's output, exit code and source commit go in the receipt.

1. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: exit 0, with T-O2, T-O3, T-C1, T-C2, T-P1, T-P2 and T-F1 in the run.
2. `bash charts/parchwell/tests/check-loadgen-host.sh` with the pinned tools: exit 0, with the final line of section 3.7 in its output and the checker's exact output asserted.
3. `./scripts/verify-dotnet.sh`: exit 0, zero warnings. The report shows `B10TimeoutEvidenceExportTests` passed and not skipped.
4. `bash infra/scripts/lifecycle/check-offline.sh`: exit 0, including the workflow byte-equality check.
5. `npm run check:docs`: no warning in the four documents of section 3.6. Other warnings take one blanket line under pull-request rule 8.
6. `cmp .github/workflows/b10-abrupt-trial.yml docs/operations/workflows/b10-abrupt-trial.yml.txt && git diff --quiet origin/main -- .github src`: exit 0.

## 10. Deployment assumptions

- DA-P1. The ordinary B10 C1 `loadgen` Job renders and runs inside the trial namespace as it does in science, because its values come from the same `Phase.values` call (T-O3 shows no diagnostic value enters). Covered offline by T-O3 and by the existing host check of B10 first-Job environments (`test_loadgen_host.py`). No new deployed-runtime assumption.
- Part 1's deployment assumptions keep their status: DA1, DA5 and the acceptance-to-kill time stay with the offline end-to-end test before the trial; DA2 to DA4 are accepted for the one trial only (decision 6769742939); DA6 and DA7 as decision 6774981258 states. This pull request delivers DA7's binding and Job names. It does not settle DA1, DA5 or DA6. The trial still waits for the offline end-to-end test (decision 6769742939).

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort scaling with a number unknown at planning: none. The export, the checker and the tests run over fixed synthetic inputs. Cold-review rounds follow pull-request rule 8's budget.
(b) Stricter than the parent: Hari merges although no protected path changes (decision 6787849492, point 2). `render_failed` stops the trial [PENDING Q5].
(c) Dependencies on code, data or behaviour outside `main` and this pull request: the deployed ordinary host and chart (DA-P1, covered offline), and part 1's assumptions (section 10). Nothing else.

## 12. Decided defaults

All defaults this plan relies on are recorded in decision 6787849492, point 4, and [PENDING Q4 and Q5].

## 13. Open questions for Hari

[PENDING: Q4, the trial run-ID prefix, and Q5, the timeout-Job failure names and render failure. Both were sent to the control session on 2026-11-11 and are not answered. When answered, this section states `None` and section 1 item 8 cites the decision.]

## 14. Sources

- ADR-021 and its "Consequences", `docs/blueprint.md` on `main` at `3a4ee0b1`.
- Part 1 plan, `items/404/plan-b10-execution-loadgen.md` at `fc1a4f36452b2d302f1d73987e1ba5eb967b3d65`, sections 3, 6 and 7.
- Code on `main` at `3a4ee0b1`: `infra/scripts/phase/b10_abrupt.py`, `infra/scripts/phase/phase.py`, `infra/scripts/phase/tests/test_b10_abrupt.py`, `charts/parchwell/tests/test_b10_abrupt_host.py`, `charts/parchwell/tests/test_loadgen_host.py`, `tests/Parchwell.LoadGen.Tests/Host/B10SecondWorkerTests.cs`, `.github/workflows/b10-abrupt-trial.yml`, `.github/workflows/dotnet-verification.yml`, `.github/CODEOWNERS`, `infra/scripts/tests/test_phase_bridge.py`.
- docs/agents/factory.md "Ruleset" for `docs/operations/` ownership.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract of section 3, a requirement of section 4, or the scope of section 2. That includes any workflow change and any verifier change.

1. A workflow or committed-copy change: stop, and return it to Hari (decision 6787849492, point 2).
2. The ordinary host does not publish one of the five required export names: stop. Do not change the ordinary host.
3. A file in section 2.1 or 2.3 changes on `main` before the pull request opens: stop and rebase only if the change is outside this plan's edits. Otherwise hand off.
