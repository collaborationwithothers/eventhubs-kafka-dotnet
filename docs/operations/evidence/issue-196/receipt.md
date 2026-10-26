# A01 verdict publication receipt

This receipt records preparation and publication checks for the proposed A01 verdict from final run 40227770436. It records measured results only for the source commits and commands stated below.

## Source and commands

The branch base and original preparation source are `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a`, plus the three authored files in this pull request. The final-run source is `fa417d783e833ad222ab5173978433448e4aefbe`. Preparation follows [plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6403053075), updated `2026-10-25T21:21:49Z`, and its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6403487457), which reports `Blocking findings: none` for the draft-preparation stage.

The official [final run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227770436), attempt 1, used that final-run source after [verification run 40227421589](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227421589) succeeded. [Candidate artifact 12613440446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227770436/artifacts/12613440446) contains `candidate.json`, SHA-256 `905faa04ef3833308629e66630e25446ba7b280e6b91379a5070ce219cb805da`. The candidate projection preserves its observed values, nulls and reasons. The [approved scenario](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fa417d783e833ad222ab5173978433448e4aefbe/scenarios/A01/scenario.yaml) and [expected outcome](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fa417d783e833ad222ab5173978433448e4aefbe/scenarios/A01/expected.yaml) are unchanged.

[Pod artifact 12613590239](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227770436/artifacts/12613590239) retains worker logs and pod JSON. Both final-run artifacts have observed expiry `2027-01-23T19:48:27Z`. Raw candidate bytes, pod JSON, private infrastructure logs and temporary validation files remain outside git. The [report](../../../../results/A01/2026-10-25.md) identifies the exact environment evidence and its node-size inference, unavailable Confluent.Kafka runtime version, missing broker-count outputs and unavailable detailed verifier projections.

The following original preparation commands exited 0 on source `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a` plus the authored three-file diff:

- `python /tmp/issue-196-acceptance.py prepare --candidate "$ISSUE196_CANDIDATE" --base "$ISSUE196_BASE"`: the real candidate hash, complete projection, all six count sums, report hash, pending gates, three-file scope and automated secret checks passed. At that preparation stage, publication was blocked pending C5 and broker-count output.
- `npm ci --ignore-scripts`: installed the locked documentation-check dependencies successfully.
- `git add results/A01/2026-10-25.json results/A01/2026-10-25.md docs/operations/evidence/issue-196/receipt.md`: staged only the approved files for documentation scanning and review.
- `npm run check:docs`: completed; both authored Markdown files had no issues. Warnings in unchanged historical files receive the blanket disposition below.
- `git diff --check` and `git diff --cached --check`: no whitespace errors.

`ISSUE196_BASE` was `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a`; `ISSUE196_CANDIDATE` was the actual extracted candidate at `/tmp/plan196-source/candidate-candidate.json`. The temporary validator is the exact body in approved Revision 3. Manual inspection of the three authored files found no credentials, connection strings, tenant/subscription IDs or raw private logs. No local .NET, infrastructure or mutation campaign is part of this documentation-only preparation. Original publish acceptance was not run at that preparation stage because its evidence inputs were absent. The continuation below applies the later publication decision; it does not claim that the original broker-count-recovery validator passed.

## Publication continuation

This continuation applies [Hari's decision of 2026-10-26](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6408210957) to approved Revision 3. It authorizes this first publication with the broker count explicitly unavailable as a deviation from ADR-019 check 2, and adopts completed C5 run 40233552992 as meeting the mutation hold. It does not approve the verdict or change the candidate, expectations, measured outcomes or final A01 run. The checked publication source is `a6f6641848eb13fcc928b8b304f5efcef1d2ca5b` plus this three-file documentation change; none of the four target source files changes.

The read-only evidence audit used `/tmp/issue196-continuation-c5/audit.py` against the official summary, run/artifact metadata and pinned Git source bytes. It exited 0, reconciled all shard status counts, recomputed both scores, verified the artifact ZIP digest and summary member hash, and established the four-file equality below. Raw artifacts and generated audit output remain outside git. The old recovery-only acceptance route is superseded by Hari's explicit deviation decision.

On publication source `a6f6641848eb13fcc928b8b304f5efcef1d2ca5b` plus the authored three-file update:

- `python3 /tmp/issue196-continuation/validate-publication.py` exited 0. It checked the unchanged final-candidate projection and count sums, accepted unavailable-count deviation and issue link, completed successful main C5 run/summary provenance, both recomputed scores, all shard/status totals, distinct report/source hashes, four-file byte identity, publication text, three-file scope and secret exclusions. It does not recover or synthesize broker counts.
- `npm run check:docs` exited 0. Both changed Markdown files had no issues; warnings in unchanged historical evidence files receive the blanket disposition below.
- `git diff --check` exited 0 with no whitespace errors.

The two temporary read-only audit/validation scripts are supplied as supplemental cold-review material, outside the tracked diff. They and all generated/raw evidence remain outside git. No .NET, A01 or mutation campaign, reaggregation, live recovery or Azure operation was run by this continuation. The final head's CI and requested Codex reviews remain separate evidence in the PR description.

## Publication gate

C5 publication status: passed. Run 40233552992, attempt 1, source `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a`, tree `263938b67f46ccc8b71dc867c4805d8a5bd73e7b`, raw score 542/542; the complete measurement passes `thresholds.break` 60.

The [completed successful main run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40233552992) produced [artifact 12616772713](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40233552992/artifacts/12616772713), named `c5-summary-40233552992-1`. Its `summary.json` SHA-256 is `0b8e5ba06ac1514d102fa01758a587a740881a6e4489ec6b39a9cafeacebd0b1`. The downloaded ZIP SHA-256 is `5e514d55ff69729a1f7f4c9e232a48bef352f37dbae851df94ba172ad40db80c`, matching the artifact API digest. The summary has `measurement_complete: true`, `incomplete_reasons: []`, `score_percent: 100.0` and `break_passed: true`.

Full score: **100.0%**, computed as (394 Killed + 148 Timeout) / 542 * 100. Killed-only score: **72.7%**, computed as 394 / 542 * 100 and rounded to one decimal place. The supplemental killed-only score is reported alongside the full score, as Hari decided; it does not replace the adopted gate. The script reconciled the status counts across all four summary shards:

| Status | Count |
| --- | ---: |
| Killed | 394 |
| Timeout | 148 |
| Survived | 0 |
| NoCoverage | 0 |
| CompileError | 27 |
| RuntimeError | 0 |
| Ignored | 35 |

The following four hashes are `summary.shards.*.report_sha256`: they identify target mutation reports, not source files. Source-file hashes are independently computed in the next section.

| Target | Mutation report SHA-256 from summary.json |
| --- | --- |
| `src/Parchwell.Verifier/Judge/PhaseJudge.cs` | `13801598eab255d415de8de4badc92c2c1d330122e429afde5394069b7da53e7` |
| `src/Parchwell.Verifier/VerifyRun/Publication/PublicationAcceptance.cs` | `e5b7053d4578cf91ebacb9edb7ed774355d7826a67d39cf7bb521f97a33436a0` |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | `10f50885b79284e650803eed1b195e5c64150427003ac5769774b46a086697b9` |
| `src/Parchwell.Verifier/VerifyRun/A01/WorkloadRecorder.cs` | `d2f3d3cc0cd59a1055d5aa51261c9ce80f287d55711587e0f1cc2e57a2934fe0` |

The C5 publication hold is met under [Hari's decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6408210957). Required checks and current-head review remain separate merge requirements.

## Target identity

A script compared the actual bytes of each target at mutation source `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a`, final A01 source `fa417d783e833ad222ab5173978433448e4aefbe`, and publication source `a6f6641848eb13fcc928b8b304f5efcef1d2ca5b`. Each target has the same SHA-256 at all three sources. This update changes only the authored result metadata, report and receipt, preserving those target bytes. The summary contains report hashes, not these independently computed source-file hashes.

| Target | Source SHA-256 at mutation, final-run and publication sources |
| --- | --- |
| `src/Parchwell.Verifier/Judge/PhaseJudge.cs` | `67768b227cce65cc1d639d03149377a51de86d39cd985624f24036e2e3f0258a` |
| `src/Parchwell.Verifier/VerifyRun/Publication/PublicationAcceptance.cs` | `8363cdebab24d14e3b65deb3d6147fee71dad6d0bebbc1b1e5332303aa94eb38` |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | `d01f8995692a84cd8bfd908b9874bd72eba08d2f737529c05c1a5d8973780806` |
| `src/Parchwell.Verifier/VerifyRun/A01/WorkloadRecorder.cs` | `065cde4de7cd27b41946b665121589f9e5b333f0d6c4814346fd4a2cc082f808` |

This establishes the four-file byte-identity condition in the [adopted identity decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6402343589). No source hash is substituted for a mutation-report hash or vice versa.

## Broker-count publication output

Broker-count publication status: unavailable, accepted for this first A01 publication by Hari. Reporting the per-client count as unavailable is an explicit deviation from ADR-019 check 2, [decided by Hari](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6408210957) and tracked in [#200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/200). The original verifier results were in the destroyed ephemeral layer and the retained candidate/pod logs do not contain the count. No count is fabricated, inferred from coverage, or replaced with zero. This limited deviation clears the broker-count publication hold; it does not amend ADR-019 for later publications or approve the verdict.

## Documentation and cold review

Documentation-check results and completed cold-review rounds are recorded here for their checked source. Final-head CI and the last cold-review result belong in the pull request description under AGENTS.md "Before opening a pull request" rule 8; this receipt need not describe its own containing commit.

Blanket disposition: documentation warnings in unchanged historical evidence files remain outside this PR's scope after Runner retirement. No changed-file warning was reported. The scan is advisory and does not establish factual accuracy; the candidate, cited source code, workflow evidence and authority decisions were checked separately.

Cold-review round 1 applied the repository's repo-review rubric to the three-file review diff and this authored receipt on the preparation source above. It independently checked the official final artifacts, run/job/log metadata, pinned source code and cited decisions. It reported the label-dependent P0 at `results/A01/2026-10-25.json:3`: both new results files require a PR labelled `verdict`, which the rubric treats as absent from a diff. Its result line was `Findings: 1 at P0 or P1`; it reported no independent factual defect or P2.

Disposition: keep the required verdict work under AGENTS.md "Before opening a pull request" rule 8's approved-plan/verdict-step exception. The PR must be labelled `verdict` and go to Hari for verdict approval and protected-path merge. The original preparation remained draft with both publication holds; the later decision above clears those holds. This kept cold-review finding itself clears no hold. No P2 prose fix or additional P2-only review round was required. The final review of this receipt update and final-head CI are recorded in the PR description.

The first-five-pull-request review window after #184 does not apply to this pull request. A REST-based script identified [#186](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/186) as that change and the next five merged pull requests as #188, #189, #191, #193 and #194, in merge-time order. This preparation adds no follow-up issue or scope beyond the approved three-file plan.
