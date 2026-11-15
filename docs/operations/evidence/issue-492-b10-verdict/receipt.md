# B10 verdict preparation receipt

This receipt describes how the B10 evidence pack in `results/B10/2026-11-15.md` and `.json` was assembled from scientific run 43689822806. Both proposed verdicts are `confirmed`: the outcomes that the merged decision rules in `scenarios/B10/expected.yaml` select for that run. Hari approves or rejects each verdict separately and merges.

## Execution card

- Deliverable: six paths (the generator, this receipt, the two results files, README line 28 and `items/492/state.md`) on branch `claude/492-b10-verdict`, as a pull request labelled `verdict`.
- Authorization: [plan Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892325880) (`items/492/plan-b10-verdict.md` at `11f8a8f181a23eb123e858fd3152441ccc90171d` on `claude/492-plan`), approved by [approval 6893052595](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6893052595), with [plan check 6893037126](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6893037126) and decisions 6892258228, 6892276924, 6892344763, 6892588990, 6892599294 and 6892899531.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G13; `npm run check:docs`; cold review (docs/agents/pull-requests.md rule 8); Codex review and Codex security review in the form hard safety rule 10 allows.
- Verification commands: the plan's section 9.
- No `scripts/verify-dotnet.sh` run: the pull request changes no file under `src/` or `tests/` (docs/agents/pull-requests.md "Verification gates").

## Generator

`make_results.py` runs checks G1 (every input hash, member hash, member manifest, journal hash and the base commit's two scenario files), G2 (C0 timeout rule recomputed), G3 (C0 root counts), G4 (C1 restart rule recomputed from the five repeats, every comparison `both`), G5 (C1 repeat 1 nonfinal problem and detail, and its source hash), G6 (no C1 root counts, `j7_counts_unavailable`), G7 (unavailable measurements are never zero), G8 (required and pinned sentences present), G9 (byte equality with the committed files in `--check`), G10 (working-tree scenario files equal the base commit's), G11 (ASCII and no leftover token), G12 (the post-diagnostic change list, per path) and G13 (the same-instance confirmation order and gap in the five restart journals). It writes nothing unless every check passes.

`--self-test` runs the real inputs once and then 23 single-change cases, each of which must fail at its named check: `hash_c0_candidate`, `hash_nonfinal_r1`, `c0_duplicate_r1`, `c0_repeat_status`, `c0_root_ok`, `c1_eligibility_r2`, `c1_pair_r3`, `c1_lost_resend_r1`, `c1_subject`, `c1_selection_boundary`, `nonfinal_detail`, `c1_root_counts_present`, `broker_zero`, `missing_sentence`, `missing_opening`, `report_edit`, `expected_yaml_edit`, `non_ascii`, `extra_change`, `wrong_attribution`, `journal_order`, `journal_sequence` and `journal_gap_low`.

## Changes made at implementation under the approval

[Approval 6893052595](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6893052595) adopts these texts, applied with no new plan revision:

1. The replacement for the pinned sentence about the restart verdict's own rule, in the report's `Restart recovery (C1)` section and in its statements section (generator constant `TEXT_ROOT_AND_RESTART`).
2. Edit a: the ASCII scan of acceptance step 7 uses `grep -rnP`.
3. Edit b: the report's inputs section and the JSON `unpublished_runs` reason say that the earlier attempt's C1 phase "failed at the start-line Job of each of its five repeats, before any producer ran". Checked here against artifact `b10-C1-pods-43686555201-1` (artifact 14150849886): its `phase-status.json` lists five start-line Jobs, all `failed`, no producer Job, and its artifact holds only those five Job folders, the final-sweep and judge folders and `phase-status.json`.
4. Edit c: two more self-test cases, `journal_sequence` and `journal_gap_low`, so the count is 23.
5. Edit d: pod rows come from the top-level Job folders only. The C1 table has 28 rows, equal to the 28 `modes[]` entries of its `phase-status.json`; the C0 table has 18.
6. The two `apply in implementation` edits of handoff 6892877778 were already in Revision 6.

## Implementer choices

- The plan says each pods-artifact member is pinned by hash in the generator. A pods artifact holds 55 (C0) and 240 (C1) members. The generator pins one digest per artifact over every member path and member SHA-256, plus the five journals individually. The ZIP hash already fixes every byte, so the manifest adds no coverage beyond naming what each pin protects.
- Running the generator without a mode flag also replaces README line 28, which must be the existing B10 row; the plan said it prints the row. It prints the row as well. `--check` requires line 28 to equal it.
- G7 is enforced on the structured values (broker counts, C1 repeat 1 counts, failed per-instance check) and on the absence of C1 root counts, and after rendering on the C1 comparison table's repeat 1 row (every cell `absent`) and the broker statement. The rendered-report part was added after cold review round 1. It has no self-test case, so the plan's count of 23 stands; a one-off probe that set one repeat 1 cell to 0 failed at G7, and nothing from that probe is committed.
- The 46 pod rows show exit codes taken from `b10_lifecycle` where the Job is listed there and otherwise from `pods.json`. A one-off scan of the report confirmed the plan's expected codes: 1 for each C0 verify Job and the C1 judge, 0 for the other 40.
- Sizes against the plan's estimate: the generator is 1162 lines (estimate about 1200), the report 214 lines (estimate 350), the JSON 1314 lines (not estimated). The pull request is within the estimate.

## Specification comparison

Each section of plan 4.3 maps to a report heading in the plan's order. The opening paragraphs, the two verdict lines, section texts and the six statements are generator constants, checked by G8. The decisions' points are recorded in plan R13. No expected outcome, verdict rule, verifier output or candidate was edited: each candidate in the JSON equals its pinned `candidate.json` member.

## Source checks made in this session (2026-11-15)

- Pinned hashes: the five ZIPs and two logs were downloaded through the REST API and `sha256sum` equalled section 4.1; the four pinned members and `expected.yaml` and `scenario.yaml` at the base commit equalled their pins.
- librdkafka v2.16.0 `INTRODUCTION.md` (SHA-256 `109a00994a9c8eed91564b11af7b95251363dac6e514740563fb4f69b378de31`) and `CONFIGURATION.md` (`b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`): every quotation in the opening paragraphs occurs in them, with whitespace normalised.
- Confluent.Kafka v2.16.0 `PersistenceStatus.cs`: `NotPersisted = 0`, `PossiblyPersisted = 1`, `Persisted = 2`.
- Microsoft transactions page: "Kafka Transactions is currently in Public preview in Premium, and Dedicated tier." Microsoft configurations page: its Java table, not its librdkafka table, holds the `enable.idempotence` row; `message.timeout.ms` is on neither; the page lists transactions as "Not currently supported".
- Pull requests 481, 484, 485, 486, 487 and 489: `git diff --name-only <merge>^1 <merge>` gave the file lists that the "Changes after the diagnostic run" text states.
- Journals: the gap from each same-instance original's terminal record to its resend's prepared record is 119.996558, 119.995184, 119.996525, 119.996190 and 119.996466 s for repeats 1 to 5.
- Before the first edit: `origin/main` equalled the plan's base `2bb71e04c17f73d08c2fcb73e85165b787cf2c99`, so the diff of the plan's changed files and external contracts between the base and current `main` is empty.

## Commands and results

Run from the repository root on the branch, with the artifacts and logs in a scratch directory outside the repository.

- `make_results.py --inputs "$IN"`: exit 0; wrote the two results files and README line 28; printed the row.
- `--check`: exit 0, `PASS check`.
- `--self-test`: exit 0; `PASS real_inputs` and 23 `PASS` lines, from `PASS hash_c0_candidate` to `PASS journal_gap_low`.
- `python3 -m json.tool results/B10/2026-11-15.json`: exit 0.
- ASCII scan `LC_ALL=C grep -rnP '[^\x00-\x7F]' results/B10/ docs/operations/evidence/issue-492-b10-verdict/ items/492/state.md README.md`: no output, exit 1.

## Cold review record

Recorded in the pull request description, not here, so that this file does not describe its own review.

## Remaining approval

Hari approves or rejects each of the two verdicts and merges. The pull request touches `/results/` and `/*.md`, protected paths.
