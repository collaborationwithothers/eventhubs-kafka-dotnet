# A04 verdict preparation receipt

This receipt describes how the A04 evidence pack in `results/A04/2026-11-03.md` and `.json` was assembled from batch run 41602168787. The proposed verdict is `undocumented`, reason `not_in_librdkafka_table`, the outcome the merged `scenarios/A04/expected.yaml` rules select for that run. The `timed_out_on_resume` class, designed after run 41534423570 was seen, decided the default configuration, and the report says so.

## Execution card

- Deliverable: six paths (the generator, this receipt, the two results files, README line 16 and `items/322/state.md`) on branch `claude/issue-322-a04-verdict`, as a draft pull request labelled `verdict`.
- Authorization: the [plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6600666591), approved under standing approval by [plan check 6600767859](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6600767859), and decisions 6563546354, 6569917526, 6588208820, 6596432846, 6602487473, 6612199147 and 6612556809 on issue 322 with the issue 301 decisions they adopt. The [plan amendment 6611936022](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6611936022) re-pins the mutation gate; [Hari's decision 6612199147](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6612199147), point 1, approves it and adopts the evidence that the hold condition is met (campaign record 6611438222 and comparison 6611864147).
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G13; `npm run check:docs`; cold review (docs/agents/pull-requests.md rule 8); Codex review in the form hard safety rule 10 allows.
- Verification commands: the plan's "Acceptance" section.
- Earliest publication checkpoint: draft pull request after the first comprehensive cold review with no unanswered P0 or P1.
- Review budget: rule 8, with the exhaustion handoff that rule defines.

## Predicate map and generator

`make_results.py` runs checks G1 (input pins), G2 (judge receipt), G3 (candidate identity), G4 (outcome), G5 (configurations and repeats), G6 (recomputed classes, summaries, assessment and the outcome without `timed_out_on_resume`), G7 (phase and batch records), G8 (pods), G9 (job log lines), G10 (mutation gate and survivor counts), G11 (scenario, expected outcome and baseline source lines), G12 (recomputed quiet periods, disconnects, Metadata requests and broker hosts) and G13 (output equality, `--check` only).

`--self-test` runs the real inputs once and then 31 single-change cases, each of which must fail at its stated check: G1-append, G1-extra, G2-exit, G2-hash, G3-notes, G3-kind, G4-hypothesis, G5-status, G5-client, G6-class, G6-summary, G6-floor, G7-stop, G7-evidence, G8-restart, G8-gap, G9-teardown, G9-bootstrap, G10-break, G10-commit, G10-survivor, G12-max, G12-disconnect, G12-azure, G12-window, G10-ancestor, G10-idle-blob, G10-config-blob, G10-config-blob-gate, G11-expected, G11-scenario. No other case exists. The last four G10 cases (added after cold review rounds 2 and 3, finding 6) replace `is_ancestor` or `git_rev` for one call and each must fail at G10: the batch source commit is not an ancestor of the measured commit; the idle judge differs between the batch source and the measured commit; the configuration judgement differs between the C5 commits, once for the earlier commit `f0850d4` (G10-config-blob) and once for the measured commit `87327f5` (G10-config-blob-gate).

The generator was decoded from the plan comment (gzip and base64, Acceptance step 0) with SHA-256 `3d92de34755b23c726317bf3f8631e01f6718d1ddfa030864671f588235eeb7a`. Five changes followed, in this order, and no change altered a check's meaning, a pinned value other than the C5 pins, a recorded number or the verdict except as stated:

1. The plan check's `apply in implementation` finding: the report title in `TEMPLATE` said the client closed idle producer connections, which the report itself lists as not tested. It now reads "each producer's first idle-window disconnect came before 225000 ms and every repeat was kept". SHA-256 after this change: `c5f20c0c4e72de9926504794cd58088b86bc6ce79ef3b167e79baf50144be50d`, the value the plan check gave.
2. The mutation gate was re-pinned from C5 run 41602164797 to C5 run 41670913768, as the plan amendment above. The new campaign measured `main` at 87327f5841822a8962dd3dbaecb2ee442a8faa8b, later than the batch run's `c878a9ab2b2550e17ee4cbb5a6388f96f93d6c62`. G10 now requires the summary identity of the new run, the measured commit's configuration (break 60, seven targets), `c878a9a` to be an ancestor of `87327f5`, and `IdleObservationJudgement.cs` to have the same git blob at both. Only the C5 pins, the per-target counts (3668 / 3813, 145 survivors, 95 beyond the cap), the gate text and G10 changed. SHA-256 after this change: `4acfcd81cc70fc645fd60f387208fa3b84551792e92df552adbde71a58258c73`.
3. Cold review round 1 (comprehensive) raised one prose finding, applied under the plan's stop rule for generator changes: the summary sentence now says that H_client_close is decided by the timing of the first idle-window disconnect whichever side closed it. SHA-256 after this change: `08c08bfdf510c4aede06a6548571b5945afd3ca19492cbfdda4cda20d97a2462`.
4. Cold review round 2 (focused) findings 2 and 6, applied under the same stop rule and Hari's decision 6612199147: the report's gate paragraph now states the shared-state mechanism as inferred, citing the hold decision, verifier-mutation.md "Issue 340 static-field isolation" and pull request 341; and `--self-test` gained the three G10 cases above. SHA-256 after this change: `60cbab818e45f40d0b2d68e399c68ab3dbcf96ba6e8c82210710720de6362afd`.
5. Cold review round 3 (focused) P2s, applied under [Hari's decision 6612556809](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6612556809), point 2: the report now says the hold decision said "its kill counts in shards containing a static-field mutant could not be relied on"; `--self-test` gained `G10-config-blob-gate`, which covers the measured-commit arm of the three-way blob check; and the command section says where the commands ran. The committed generator's SHA-256 is now `2d450c9c9b0b19f607c0fb424030c8a046bdfe979c5c0ef7efe90a28dd40e207`.

After each change Acceptance steps 2 to 8 were rerun; the last rerun is in "Commands and results".

## Specification comparison

Each "Evidence pack" bullet maps to a report section: the approved scenario and expected outcome (Approved inputs and run); per-repeat results (Repeat outcomes and the JSON `candidate`); validity, completion and pod evidence (Repeat outcomes, Versions, environment and timing); verifier counts (stated not applicable, absent); broker counts (Disconnects recomputed from the statistics reports); versions, PU count, region and timestamps (Versions, environment and timing); the proposed verdict and summary (opening paragraph, Assessment against expected.yaml). The `text_must_state` clauses of A04 expected.yaml map to the Assessment section, the resume timeout class section and the Not tested section.

## Evidence and claim verification

Every input is pinned by SHA-256 in the generator and re-checked by G1. The candidate ZIP, pod ZIP and two log hashes equal the plan's Acceptance step 1 values. The C5 summary ZIP is not the plan's: the plan pinned artifact 13220181257 of run 41602164797, and the re-pin replaced it with artifact 13256330812 (`c5-summary-41670913768-1`) of run 41670913768, whose ZIP SHA-256 is `fdd649f14cd26fffbb58bae37b260cba2cc3ac44c628db991d13141491cedfa9`. The plan's `.json` and `.md` reference hashes no longer apply to the outputs. The `.json` output changed only under `publication.mutation_gate`; its SHA-256 is `831d451e299a138d54d6254a68eb7138c707199a25d2ede567d59a0c5c05e424`. The `.md` output SHA-256 is `f807b1a7575a01531ccb6b68fc827b6692c2ed99c36e735b056de0e0ff5d0113`. No survivor review or replay was run: the report lists the 145 survivors as unreviewed, 23 of them in the judge, with the first 50 as the disposition sample and 95 beyond the cap.

## Commands and results

Run from the repository root on the branch tree based on `main` 87327f5841822a8962dd3dbaecb2ee442a8faa8b with the artifacts and logs in a scratch directory outside the repository. The generator exists only on this branch, not on `main`.

- Acceptance step 0: decode exit 0; `sha256sum` printed `3d92de34755b23c726317bf3f8631e01f6718d1ddfa030864671f588235eeb7a` for the plan's original generator (the committed generator has changed since, see above).
- Acceptance step 1, as amended: `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13218410622/zip`, `.../artifacts/13218221146/zip`, `.../artifacts/13256330812/zip` (the re-pinned C5 summary, replacing 13220181257), `.../actions/jobs/125803257704/logs` and `.../actions/jobs/125826936334/logs` each exited 0; extraction exited 0. `sha256sum` printed the plan's values for the candidate ZIP (`99b0f3e3...ec63`), pod ZIP (`2233339c...b459`), batch log (`a747f60c...5632`) and stop log (`76bd6dde...afe6`), and `fdd649f1...dfa9` for the C5 ZIP.
- Generator with no mode flag: exit 0; wrote only the two results files, the `.json` unchanged and the `.md` changed in the gate paragraph.
- `--readme-row`: exit 0; one line, identical to README line 16.
- `--check`: exit 0.
- `--self-test`: exit 0; 31 `PASS` lines from `PASS G1-append` to `PASS G11-scenario`, including `PASS G10-ancestor`, `PASS G10-idle-blob`, `PASS G10-config-blob` and `PASS G10-config-blob-gate`.
- `npm run check:docs`: exit 0 with 8 warnings, all in files this pull request does not change (docs/agents/factory.md, docs/operations/evidence/issue-124, issue-140 and issue-211).
- Acceptance step 7: after `git add` of exactly the six paths, `git fetch origin main && git diff --cached --merge-base origin/main --name-only` exited 0 and listed exactly `README.md`, `docs/operations/evidence/issue-322-a04-verdict/make_results.py`, `docs/operations/evidence/issue-322-a04-verdict/receipt.md`, `items/322/state.md`, `results/A04/2026-11-03.json` and `results/A04/2026-11-03.md`.
- Acceptance step 8: the ASCII scan `LC_ALL=C grep -nP '[^\x00-\x7F]'` over the report, the evidence folder, `README.md` and `items/322/state.md` printed nothing (grep exit 1).

## Branch history

The branch `claude/issue-322-a04-verdict` was not rewritten ([decision 6612556809](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322#issuecomment-6612556809), point 3). Its head before this session was `5434e0e2047be00180e1cf3a4317bde65807fedf`, based on `main` 69102fe. `main` at 87327f5 was merged into that head without conflicts, and the corrected files of this pull request were then committed on top, so every push is a fast-forward. The same corrections were first saved as commit 92ebe37 on the backup branch `claude/issue-322-a04-verdict-wip`, which was a rebase of the old commits and is not part of this branch's history. The review diff is the tree against `main`, so it is unaffected by the merge.

## Cold review record

Recorded in the pull request description, not here, so that this file does not describe its own review.

## Remaining approval

Hari reviews. The #301 control session may merge with the `verdict` label under decision 6563540913 points 1 and 2, only when every condition of decision 6563515099 point 1 holds.
