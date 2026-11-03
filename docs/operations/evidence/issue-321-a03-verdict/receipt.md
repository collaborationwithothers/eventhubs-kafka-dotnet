# A03 verdict preparation receipt

This receipt describes how the A03 evidence pack in `results/A03/2026-11-02.md` and `.json` was assembled from batch run 41534423570. The proposed verdict is `confirmed`, the word the merged `scenarios/A03/expected.yaml` rules select for that run. The run also differs from the expected outcome in one configuration, which the report publishes as a finding.

## Execution card

- Deliverable: six paths (the generator, this receipt, the two results files, README line 15 and `items/321/state.md`) on branch `claude/issue-321-a03-verdict`, as a draft pull request labelled `verdict`.
- Authorization: the [plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/321#issuecomment-6596244204), approved under standing approval by [plan check 6596353410](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/321#issuecomment-6596353410), and decisions 6563546186, 6569917266 and 6596020684 on issue 321 with the issue 301 decisions they adopt.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G13; `npm run check:docs`; cold review (docs/agents/pull-requests.md rule 8); Codex review in the form hard safety rule 10 allows.
- Verification commands: the plan's "Acceptance" section.
- Earliest publication checkpoint: draft pull request after the first comprehensive cold review with no unanswered P0 or P1.
- Review budget: rule 8, two comprehensive passes and two focused rounds; exhaustion action is the handoff to Hari that rule 8 defines.

## Predicate map and generator

`make_results.py` runs checks G1 (input pins), G2 (judge receipt), G3 (candidate identity), G4 (outcome), G5 (configurations and repeats), G6 (recomputed classes, summaries and assessment), G7 (phase and batch records), G8 (pods), G9 (job log lines), G10 (mutation gate and 38 survivor dispositions), G11 (scenario, expected outcome and baseline source lines), G12 (recomputed quiet periods, disconnects and Metadata requests) and G13 (output equality, `--check` only).

`--self-test` runs the real inputs once and then 25 single-change cases, each of which must fail at its stated check: G1-append, G1-extra, G2-exit, G2-hash, G3-notes, G3-kind, G4-outcome, G5-status, G5-client, G6-class, G6-hypothesis, G6-c2-summary, G7-stop, G7-evidence, G8-restart, G8-gap, G9-teardown, G10-break, G10-commit, G10-survivor, G12-max, G12-metadata, G12-window, G11-expected, G11-scenario. No other case exists. The plan check's `apply in implementation` finding fixed the signature `check_g11(sources, expected)`, which cases 24 and 25 call.

## Specification comparison

Each "Evidence pack" bullet maps to a report section: the approved scenario and expected outcome (Approved inputs and run); per-repeat results (Repeat outcomes and the JSON `candidate`); validity, completion and pod evidence (Repeat outcomes, Versions, environment and timing, the jobs table); verifier counts (stated not applicable, absent); broker counts (`maximum_up_brokers` column); versions, PU count, node size, region and timestamps (Versions, environment and timing); the proposed verdict and summary (opening paragraph, Assessment). The `text_must_state` clauses of expected.yaml map to the threshold sentence and the windows, repeat-outcomes and client-errors tables.

## Evidence and claim verification

Every input is pinned by SHA-256 in the generator and re-checked by G1. The ZIP and log hashes were checked against the plan's Acceptance step 1. The generator output hashes equal the plan's reference values: `.md` `afd22b5152d4d666bfb66bf699c44427cf645fc24aa6e31b76b91ab71b836092`, `.json` `821f16f8ae7e3bf78ecb362da85970907f12bdefbe1958a3c6758c37ea20468f`. Survivor dispositions are those already recorded in docs/operations/verifier-mutation.md for run 41232286130 at the same locations; no replay was run. Node size is stated as inferred.

## Commands and results

Run from the repository root at base `9014b8effd4f8556d3446bcfbf8f951a52905bb2` with the artifacts and logs in a scratch directory outside the repository.

- The five `sha256sum` values equal the plan's Acceptance step 1.
- Generator with no mode flag: exit 0; wrote only the two results files.
- `--readme-row`: exit 0; one line, used as README line 15.
- `--check`: first run exit 1 with `FAIL G13: README.md line 15 differs` (README not yet updated); after the README change, exit 0.
- `--self-test`: exit 0; 25 `PASS` lines from `PASS G1-append` to `PASS G11-scenario`.
- `npm run check:docs`: exit 0 with 8 warnings, all in files this pull request does not change (docs/agents/factory.md, docs/operations/evidence/issue-124, issue-140 and issue-211).

## Cold review record

Recorded in the pull request description and the self-review record, not here, so that this file does not describe its own review.

## Remaining approval

Hari reviews. The #301 control session may merge with the `verdict` label under decision 6563540913 points 1 and 2, only when every condition of decision 6563515099 point 1 holds.
