# C5 timing margin

Revision R1. Base `e62bdc9b39620897c63e615b059d20646b0546e4`. Measured-input candidate `5d70de86ca7655549bd3a8f5f8c98910385c492a`.

## Goal and authority

Close #494 after complete C5 acceptance on qualified `main`. The 23 November campaign delivered 323 of 324 physical receipts and took 3,737 seconds. The hosted runner for `phase-p1` shut down before uploading its receipt. The companion alone took 3,509 seconds. The slowest command grew from 1,547.778 to 3,466.014 seconds across the same 57 mutations and outcomes. The owner approved resizing from these timings and one complete pair after the protected product merge. All 324 physical receipts, the 60% score floor and the inclusive 3,000-second end-to-end bound remain required. [Decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7123218757) · [Terminal evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7115998357)

## Ordered split and scope

1. **Pin the timing diagnostic.** Change only `.github/workflows/verifier-c5-sizing.yml`, its byte-identical mirror at `docs/operations/workflows/verifier-c5-sizing.yml.txt`, and `docs/operations/verifier-c5-sizing.md`. Replace the two existing source pins and the include matrix with the exact candidate and table below. Keep the previous source, matrix size, and run link as runbook history. An estimate on 24 November, based on the old 31-row file and the new 38-row table, is three files and about 120 changed lines.
2. **Qualify the measured cuts.** Change only the cut table and its equivalent expected-cut assertions in `tests/Parchwell.Verifier.Tests/Mutation/{c5_workflow.py,test_c5_workflow.py}`, the checker and its tests in `{c5_runtime.py,test_c5_runtime.py}`, `c5-runtime-qualification.json`, and `docs/operations/verifier-mutation.md`. The immutable candidate contains the two table edits. Bring its actual commit into the product PR history and preserve it using a merge commit. An estimate on 24 November, based on the 60 table-line diff, 38 evidence entries, the prior observed-tail proof and matching regression cases, is six files and about 1,500 changed lines.

The role matrices, aggregate workflow, target and test-project source, configuration defaults, static and cohort ownership, corpus, mutation thresholds, Azure workflows and scientific results stay outside the file set. The owner merges both protected PRs after their exact-head CI and reviews complete. [Protected paths](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/e62bdc9b39620897c63e615b059d20646b0546e4/.github/CODEOWNERS#L7-L19)

## External contracts and invariants

- The diagnostic retains its `workflow_dispatch` trigger, required `source_sha`, main-only job condition, standard `ubuntu-24.04` runner, read-only contents permissions, 50-minute job timeout, 2,650-second inner and 2,700-second outer command limits, statuses and evidence format. Keep one-day retention and existing separate RPC archive. [Existing workflow](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/e62bdc9b39620897c63e615b059d20646b0546e4/.github/workflows/verifier-c5-sizing.yml)
- The product retains checker CLI, result statuses and reason names, v2 JSON shape and evidence fields. The proof is specific to an immutable measured transition and validates renewable assessments against actual input maps on the checked head. Diagnostic receipts supply new measurements only for the exact 38 changed selectors. Retained items continue under their prior qualified provenance. [Checker](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/e62bdc9b39620897c63e615b059d20646b0546e4/tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py#L2150-L2238)
- The acceptance campaign retains 178 coordinator and 146 companion physical jobs, one complete aggregate, exact identity and attempt binding, full disjoint shard ownership and existing score rules. The 3,000 seconds include the elapsed time from the earlier run creation through the later terminal completion. [Terminal checker](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/e62bdc9b39620897c63e615b059d20646b0546e4/tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py#L236-L285)

## Requirements

1. When constructing the measurement candidate, change only the listed primary cut offsets and the matching expectation literals. Compare the canonical selection bytes for all 324 jobs to the base. Require the exact 38 differences below, the same ordered role membership, unchanged target source and executable runner, and identical static and cohort ownership. A crossing selector may pick a different set of ordinary mutations after its cut move. The source ranges still give every native mutation exactly one physical owner.
2. When the diagnostic PR is ready, verify that the exact source is reachable, each matrix target SHA-256 equals the candidate blob, each row matches a changed physical configuration, both pins match the candidate, and the mirror is identical. After the protected merge dispatch one first-attempt diagnostic on exact `main`. A failed or missing row leaves this candidate unqualified.
3. When the diagnostic ends, check each job and attempt identity, provider ZIP size and SHA-256, exact candidate, source tree and target, canonical configuration, source and projection bindings, expected static isolation and all child outcomes. Check raw command and report digests and individual timed identities. Compare the complete native identity multiset by mutator, replacement, location and static flag to the authenticated latest campaign, since numeric IDs can vary across runs. Establish the executed owners from the candidate ranges. Require an observation above zero and at most 2,650 seconds with one or two verified workers, an estimate at least as large as the observation, and a rounded estimate at most 2,700 seconds. A missing digest, mismatched population or estimate above either permitted bound leaves qualification incomplete.
4. When checking the product head and its supported descendants, prove prior-to-candidate-to-head ancestry, exact old and new compiled selector intent, unchanged ordered role matrices and normalized runtime inputs, and the exact selector set. Continue to reject nonrenewable changes. Match the prior ledger and keep exact items and timing records for all 286 unchanged selectors. Preserve original context source, measured maps, still-current reviewed assessments and complete existing target evidence as a prefix. Append one authenticated receipt per changed selector and pin a canonical digest of the exact shard, target, item, timing and artifact association. Keep the original context where a normalized map already has one, and separately bind fresh timing to the new immutable candidate. Validate retained first-bridge and older timing at their original proof anchors, and validate current assessments independently on the actual head. The prior ledger remains the immutable qualification anchor rather than new measurement.
5. When input maps on a later descendant differ only in a supported renewable input, accept a correct v2 assessment and reject a missing one or an assessment with a stale or incorrect subject digest. The exact-head checker rejects swapped/missing receipts, geometry and ancestry drift, and attempts to reuse prior receipts for changed selectors. When an existing fixture needs adjustment, keep every assertion and boundary, or keep its intentionally invalid input and assert the exact resulting rejection. Record each fixture adjustment in the PR.
6. When the product PR merges, verify the checkout equals exact current `main`, the protected merge includes the measurement and reviewed commits, and the committed qualification returns `qualified` for the full ledger. Dispatch the companion once, record its exact run and attempt, then dispatch the coordinator once against the same main commit and pass those values. Wait for both and the aggregate. Check provider ZIP size and digest, run the committed terminal checker and count the expected physical receipts against the summary. Close #494 only on both successful workflows, every physical receipt and verified score at least 60%, with the terminal checker returning `met`. Preserve failures and the first measurement on a miss.

## Exact diagnostic matrix and cut changes

| Physical shard | Target | Target SHA-256 |
|---|---|---|
| `b10-final-judge-crossing` | `Judge/B10FinalJudge.cs` | `e37f0604e2483e2c54f73006dca1e6fc68f5ee5e422a660ecd18f94f0240967a` |
| `b10-final-judge-p11` | `Judge/B10FinalJudge.cs` | `e37f0604e2483e2c54f73006dca1e6fc68f5ee5e422a660ecd18f94f0240967a` |
| `b10-final-judge-p12` | `Judge/B10FinalJudge.cs` | `e37f0604e2483e2c54f73006dca1e6fc68f5ee5e422a660ecd18f94f0240967a` |
| `b10-final-projection-codec-crossing` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-projection-codec-p10` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-projection-codec-p13` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-projection-codec-p14` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-projection-codec-p7` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-projection-codec-p8` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-projection-codec-p9` | `Common/Phase/B10FinalProjectionCodec.cs` | `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` |
| `b10-final-repeat-evidence-crossing` | `VerifyRun/B10Final/B10FinalRepeatEvidence.cs` | `b14f190b6aa6c705a03647b4dc09edb5ba8ca6cea193f76c64a09d089278cfc1` |
| `b10-final-repeat-evidence-p1` | `VerifyRun/B10Final/B10FinalRepeatEvidence.cs` | `b14f190b6aa6c705a03647b4dc09edb5ba8ca6cea193f76c64a09d089278cfc1` |
| `b10-final-repeat-evidence-p2` | `VerifyRun/B10Final/B10FinalRepeatEvidence.cs` | `b14f190b6aa6c705a03647b4dc09edb5ba8ca6cea193f76c64a09d089278cfc1` |
| `b10-intake-crossing` | `VerifyRun/Judgement/B10IntakeChecks.cs` | `ade1802b795c9313ded04dbf1332752a0bc8c594473854519aecd54c6145b8ab` |
| `b10-intake-p1` | `VerifyRun/Judgement/B10IntakeChecks.cs` | `ade1802b795c9313ded04dbf1332752a0bc8c594473854519aecd54c6145b8ab` |
| `b10-intake-p2` | `VerifyRun/Judgement/B10IntakeChecks.cs` | `ade1802b795c9313ded04dbf1332752a0bc8c594473854519aecd54c6145b8ab` |
| `b10-restart-manifest-crossing` | `VerifyRun/Input/RestartManifestReader.cs` | `d48a59bcbe7af3a60b00555b2b34f0faf40c671dbbfbce951ad835cc17000598` |
| `b10-restart-manifest-p2` | `VerifyRun/Input/RestartManifestReader.cs` | `d48a59bcbe7af3a60b00555b2b34f0faf40c671dbbfbce951ad835cc17000598` |
| `b10-restart-manifest-p3` | `VerifyRun/Input/RestartManifestReader.cs` | `d48a59bcbe7af3a60b00555b2b34f0faf40c671dbbfbce951ad835cc17000598` |
| `b10-restart-manifest-p4` | `VerifyRun/Input/RestartManifestReader.cs` | `d48a59bcbe7af3a60b00555b2b34f0faf40c671dbbfbce951ad835cc17000598` |
| `phase-candidate-codec-crossing` | `Judge/PhaseCandidateCodec.cs` | `02c1dd85b444bdb0b8e33027e4d8f6af178a85363a7e184199ead304d18b8905` |
| `phase-candidate-codec-p19` | `Judge/PhaseCandidateCodec.cs` | `02c1dd85b444bdb0b8e33027e4d8f6af178a85363a7e184199ead304d18b8905` |
| `phase-candidate-codec-p20` | `Judge/PhaseCandidateCodec.cs` | `02c1dd85b444bdb0b8e33027e4d8f6af178a85363a7e184199ead304d18b8905` |
| `single-action-configuration-crossing` | `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | `e266b1d73da52fe84fbb583bac42e8bd5e2118af445d8a2f3c4bf7c4ff67f661` |
| `single-action-configuration-p40` | `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | `e266b1d73da52fe84fbb583bac42e8bd5e2118af445d8a2f3c4bf7c4ff67f661` |
| `single-action-configuration-p41` | `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | `e266b1d73da52fe84fbb583bac42e8bd5e2118af445d8a2f3c4bf7c4ff67f661` |
| `single-action-configuration-p42` | `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | `e266b1d73da52fe84fbb583bac42e8bd5e2118af445d8a2f3c4bf7c4ff67f661` |
| `verifier-result-codec-crossing` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p17` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p18` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p21` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p22` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p23` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p24` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verifier-result-codec-p25` | `Common/Results/VerifierResultCodec.cs` | `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` |
| `verify-run-handler-crossing` | `VerifyRun/VerifyRunHandler.cs` | `2a9d721f3b7f55e6aa4b7c19f4871278712dacba0549ac27dfe2a5f70a632c8e` |
| `verify-run-handler-p6` | `VerifyRun/VerifyRunHandler.cs` | `2a9d721f3b7f55e6aa4b7c19f4871278712dacba0549ac27dfe2a5f70a632c8e` |
| `verify-run-handler-p7` | `VerifyRun/VerifyRunHandler.cs` | `2a9d721f3b7f55e6aa4b7c19f4871278712dacba0549ac27dfe2a5f70a632c8e` |

Cuts use one-based boundary indices and UTF-16 offsets. Every unlisted offset stays as in the immutable base.

| Target key | Boundary index and previous → new offset |
|---|---|
| `b10-final-judge` | 11 27524 → 27568 |
| `b10-final-projection-codec` | 7 8544 → 8642, 9 11342 → 11070, 13 16156 → 16272 |
| `b10-final-repeat-evidence` | 1 12311 → 13730 |
| `b10-intake` | 1 4158 → 3617 |
| `b10-restart-manifest` | 2 2044 → 1435, 3 6937 → 3637 |
| `phase-candidate-codec` | 19 29996 → 29526 |
| `single-action-configuration` | 40 41130 → 41174, 41 43393 → 42912 |
| `verifier-result-codec` | 17 39474 → 39804, 21 50624 → 50715, 23 57576 → 56963, 24 62111 → 62006 |
| `verify-run-handler` | 6 30029 → 26909 |

## Test map and acceptance

- **Diagnostic source and layout, requirements 1–2.** Compare all 324 actual canonical selectors against the base. Exactly the table entries change. Mutate a source pin or one table digest in the audit input and require a mismatch. Confirm plain source and generated mirror are byte-identical. Existing fixture asserts the literal cut tuples, range boundaries, disjoint ownership and unchanged static/cohort assumptions. Hosted CI runs the repository tests.
- **Receipt and proof binding, requirements 3–4.** Verify all 38 hosted receipts with provider and command-log hashes. In the current-base ledger regression, swap receipt digests within a target, substitute the first repair's source for a changed selector, alter a prior record or its referenced measured maps, remove or add a physical record, and remove the measured ancestry. Each must reject. Keep the prior bridge and historical records intact in the passing case. Isolate receipt-content authentication while exercising observation 2,650 versus 2,651, estimate 2,700 versus 2,701, and observation one second above its smaller estimate. The passing limit values and their adjacent rejections remain distinct.
- **Renewal, requirement 5.** Exercise the prior real v2 context including the merged snapshot frontier assessment. Accept an actual renewed context with exact assessed subject bytes and reject the same input with the assessment missing or stale. Cover ignored/unaccounted input and immutable worker or matrix drift through the existing final-head checker.
- **Terminal acceptance, requirement 6.** Retain and authenticate the aggregate, then use `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py terminal --repository collaborationwithothers/eventhubs-kafka-dotnet --commit <main-sha> --manifest <campaign.json> --summary <summary.json> --output <performance.json>`. Require `status=met`, `elapsed_seconds<=3000`, both workflows successful, 324 manifest-bound physical receipts and score at least 60%. Existing tests cover 3,000 versus 3,001 seconds and unsuccessful or missing job evidence. Preserve both terminal evidence and the slowest critical-path step on failure. Hosted CI provides the PR tests per the owner's direction. [CI direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894)

## Deployment assumptions

The observed runner speeds vary across fresh standard hosted runners. The sizing study uses 44 latest, 36 prior exact-selector and eight earlier exact-selector diagnostic archives, validated by provider hashes, with semantic matching and the larger observed duration per mutation, rounded up for log precision. Fresh diagnostic and final-pair evidence are required. The setup/overhead comes from recorded fresh-run commands, and the previous aggregate's post-peer work and upload took 210 seconds. The unchanged restart manifest p1 ran ten serial isolated mutations and took 2,652.160 seconds in the last attempt. That retained selector is an observed campaign risk. Its proof and result acceptance do not imply a bound on future runner variability. No Azure connection or price assumption is used.

## Stop rule and caps

Model at most 108 archived receipts with up to 500 MB of selected provider ZIPs from the three authenticated input sets. This study used 88 archives. Stop the candidate and record the gap on missing evidence or infeasible timing under the cap. Run one 38-job diagnostic and one 324-job acceptance pair. If the diagnostic misses its receipt or timing bounds, preserve the output and report the gap. If the campaign is incomplete or exceeds 50 minutes, retain the artifact and critical-path evidence and keep #494 open. These caps do not authorize a second dispatch or a change to the cutoff or score gate.

## Plan-check questions

1. **Does it solve the issue it claims?** It redistributes the measured bottlenecks and obtains fresh source-bound proof. The full measured campaign decides closure under requirements 3 and 6.
2. **Is the approach within scope and safe?** Requirements 1–5 preserve all physical ownership coverage, parent proof, static isolation, v2 checks and the standard-runner path. The two merges have independent review and CI gates.
3. **Can an implementer execute it without making contract decisions?** The immutable source, exact table, ledger interface, limits, tests, approved workflow order and failure behavior pin the contract. The code structure remains the implementer's choice.

## Sources

- [Failed campaign and root evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7115998357)
- [Earlier successful population with timing miss](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7080705681)
- [Earlier observed-tail qualification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/590)
- [Prior hosted diagnostic](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45117421809)

## Open questions for Hari

None.
