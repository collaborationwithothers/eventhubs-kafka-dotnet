# A04 verdict preparation receipt

This receipt describes how the A04 evidence pack in `results/A04/2026-11-03.md` and `.json` was assembled from batch run 41602168787. The proposed verdict is `undocumented`, reason `not_in_librdkafka_table`, the outcome the merged `scenarios/A04/expected.yaml` rules select for that run. The `timed_out_on_resume` class, designed after run 41534423570 was seen, decided the default configuration, and the report says so.

## Execution card

- Deliverable: six paths (the generator, this receipt, the two results files, README line 16 and `items/322/state.md`) on branch `claude/issue-322-a04-verdict`, as a draft pull request labelled `verdict`.
- Authorization: the [plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6600666591), approved under standing approval by [plan check 6600767859](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6600767859), and decisions 6563546354, 6569917526, 6588208820 and 6596432846 on issue 322 with the issue 301 decisions they adopt.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G13; `npm run check:docs`; cold review (docs/agents/pull-requests.md rule 8); Codex review in the form hard safety rule 10 allows.
- Verification commands: the plan's "Acceptance" section.
- Earliest publication checkpoint: draft pull request after the first comprehensive cold review with no unanswered P0 or P1.
- Review budget: rule 8, with the exhaustion handoff that rule defines.

## Predicate map and generator

`make_results.py` runs checks G1 (input pins), G2 (judge receipt), G3 (candidate identity), G4 (outcome), G5 (configurations and repeats), G6 (recomputed classes, summaries, assessment and the outcome without `timed_out_on_resume`), G7 (phase and batch records), G8 (pods), G9 (job log lines), G10 (mutation gate and survivor counts), G11 (scenario, expected outcome and baseline source lines), G12 (recomputed quiet periods, disconnects, Metadata requests and broker hosts) and G13 (output equality, `--check` only).

`--self-test` runs the real inputs once and then 27 single-change cases, each of which must fail at its stated check: G1-append, G1-extra, G2-exit, G2-hash, G3-notes, G3-kind, G4-hypothesis, G5-status, G5-client, G6-class, G6-summary, G6-floor, G7-stop, G7-evidence, G8-restart, G8-gap, G9-teardown, G9-bootstrap, G10-break, G10-commit, G10-survivor, G12-max, G12-disconnect, G12-azure, G12-window, G11-expected, G11-scenario. No other case exists.

The generator was decoded from the plan comment (gzip and base64, Acceptance step 0) with SHA-256 `3d92de34755b23c726317bf3f8631e01f6718d1ddfa030864671f588235eeb7a`. The plan check's `apply in implementation` finding was then applied: the report title in `TEMPLATE` said the client closed idle producer connections, which the report itself lists as not tested. It now reads "each producer's first idle-window disconnect came before 225000 ms and every repeat was kept". The committed generator's SHA-256 is `c5f20c0c4e72de9926504794cd58088b86bc6ce79ef3b167e79baf50144be50d`, the value the plan check gave. No check, pinned value, recorded number or verdict changed.

## Specification comparison

Each "Evidence pack" bullet maps to a report section: the approved scenario and expected outcome (Approved inputs and run); per-repeat results (Repeat outcomes and the JSON `candidate`); validity, completion and pod evidence (Repeat outcomes, Versions, environment and timing); verifier counts (stated not applicable, absent); broker counts (Disconnects recomputed from the statistics reports); versions, PU count, region and timestamps (Versions, environment and timing); the proposed verdict and summary (opening paragraph, Assessment against expected.yaml). The `text_must_state` clauses of A04 expected.yaml map to the Assessment section, the resume timeout class section and the Not tested section.

## Evidence and claim verification

Every input is pinned by SHA-256 in the generator and re-checked by G1. The ZIP and log hashes equal the plan's Acceptance step 1 values. The `.json` output hash equals the plan's reference value `93bc09fb2346ae92d7e5d6185dac6c0e5c66453312a93ff1ec2962b4c30437dc`. The `.md` hash differs from the plan's reference value, as the plan check said it would after the title change, and is `f9a39bbe81918d43095e0c4090b917245e7afe75f1b7a19ce343616eb5738076`. No survivor review or replay was run: the report lists the 89 survivors as unreviewed, 14 of them in the judge, with the first 50 as the disposition sample and 39 beyond the cap.

## Commands and results

Run from the repository root at base `69102fe874698ab16839fe789b0bb54f158d535a` with the artifacts and logs in a scratch directory outside the repository.

- Acceptance step 0: decode exit 0; `sha256sum` printed `3d92de34755b23c726317bf3f8631e01f6718d1ddfa030864671f588235eeb7a`.
- Acceptance step 1: the five `gh api` downloads each exited 0 and the extraction exited 0. `sha256sum` printed the plan's five values.
- Generator with no mode flag: exit 0; wrote only the two results files.
- `--readme-row`: exit 0; one line, used as README line 16.
- `--check`: exit 0 after the README change.
- `--self-test`: exit 0; 27 `PASS` lines from `PASS G1-append` to `PASS G11-scenario`.
- `npm run check:docs`: exit 0 with 8 warnings, all in files this pull request does not change (docs/agents/factory.md, docs/operations/evidence/issue-124, issue-140 and issue-211).
- Acceptance steps 7 and 8 are recorded in the pull request description after staging.

## Cold review record

Recorded in the pull request description, not here, so that this file does not describe its own review.

## Remaining approval

Hari reviews. The #301 control session may merge with the `verdict` label under decision 6563540913 points 1 and 2, only when every condition of decision 6563515099 point 1 holds.
