# F04 final-run verdict preparation receipt

This receipt records preparation of the proposed F04 verdict from final filtered run 40722187672. It describes checks on source commit `20f17794330b60d77a75bf5788ee442cc8baa1f6` plus the authored publication files, before their containing commit is created.

## Authority and scope

Hari directed the current Codex session to create a separate C08 investigation and prepare the F04 verdict pull request. The [parent scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6477157262) and [child adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/230#issuecomment-6477157742) record that direction. They authorize preparation, not approval or merge of the proposed verdict. C08 is excluded and tracked in [issue #235](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235).

Scientific rules remain the [parent plan Revision 7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415356278), its [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6454346600), and the approved S3 amendment implemented in [PR #231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/231). This is the verdict step of factory.md "Item lifecycle" step 5, with no separate implementation-grade plan. No scenario, expected outcome, verifier, workflow or earlier published result is changed. No additional live execution is authorized; both filtered dispatches are consumed.

The verdict branch is `codex/issue-230-f04-verdict`, based on `origin/main` at the source commit above. The candidate-derived JSON and report concern only the final run. There is no pooling of earlier repeats and no result for C08.

## Evidence and claim verification

The source is [run 40722187672](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40722187672), attempt 1. The overall run failed at C08 C1. All five F04 candidates and their successful judge receipts were accepted. A read-only high-effort agent independently audited the five candidates, five receipts and eleven F04 seed/capture/judge pods before publication assembly.

| Artifact | ID | Downloaded ZIP SHA-256 |
| --- | ---: | --- |
| Final candidates | 12834018507 | `a59b5cb6c9acaacb455eb2b9ef662961a67f89c8f15ae8dd3cfa30e46493bd14` |
| Final pod evidence | 12835061323 | `bd40891e17c72c7fce5b701e992fb424dafcfc42b1e1f15e7fcbd2a07c0e8bef` |
| Applicable C5 summary | 12808596014 | `0d16bc3acb830b1691efd1c517a6c34bd72e7e0314e60faeab6e23929d0bc83a` |

The generator pins the F04 artifact members and approved inputs by SHA-256, retains candidate and judge-receipt content without alteration, and re-serializes it into the result. Its supported inputs, failure conditions and negative example are in its docstring. The verdict derivation is transcribed from the pinned expected outcome; it is not a new scientific predicate.

The independent audit and scripted assembly establish the same measured classes in all five repeats of each configuration: C0/C4 success, C1 broker error 29, C2 no_outcome and C3 local error -169. All 25 repeats are complete_valid with no reasons. The verifier's final item assessment is undocumented, matching the transcribed derivation. ADR-012 totals are not applicable and remain absent, never substituted with zeros. Checked broker-count values and statistics references are retained, including the observed zero counts in C3.

The authorized enrichment seed delivered the known document-state marker before the tests. Its receipt binds the correct run, source, Job, pod name/UID, service account, topic, partition, key, value hash and successful delivery/cleanup. It is independent of C1's failed write. Same-run C0 produces the document-events marker required by C4. These prerequisites answer the possibility that the consumer merely had no seeded data available at setup.

C2 is reported only as "no read and no error within the window"; it is no authorization result. Its five client-log cap markers each report 667 dropped lines, so the debug-based cause is unavailable even though pod collection itself has no gap. The wrong-resource token in C3 is distinct from missing target roles in C1/C2.

Numeric error names were checked against current reads of pinned [librdkafka v2.16.0 rdkafka.h](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.h): line 495 defines 29 as TOPIC_AUTHORIZATION_FAILED, and line 352 defines -169 as __AUTHENTICATION. The report states the observed classes and does not make a universal documentation or authorization claim. Identity placement is cited to the run-source Terraform; no Azure credential was used in this session.

PU count, region and tier are observed in the final batch log's teardown plan. Node size is explicitly an inference from the pinned cluster configuration and its no-change plan, rather than a measured node SKU. Pod timings, versions, digests, collection status and artifact expiry are retained in the result and report. Cleanup is confirmed by `ephemeral_state_empty` in the batch log and `aks_stopped` in the separate stop-job log.

[C5 run 40642331044](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40642331044) measured merge commit `1f14866faf395ecb28d3b9009986c1b332074386`, which is an ancestor of this live source. The actual summary records all six shards, measurement_complete true, break_passed true and 1341/1341 in the raw score population. A scripted diff confirms that all six configured mutation targets, the C5 configuration and its workflow are unchanged between the measured and live commits. No new mutation run was dispatched.

## Commands and results

Checks ran from the verdict worktree at the source commit stated above, plus these four authored files. Commands use the extracted final artifacts under `/tmp/postmerge-230/live2/` and the downloaded C5 summary under `/tmp/postmerge-230/c5-summary/`:

| Command or bounded check | Result |
| --- | --- |
| `python3 -I docs/operations/evidence/issue-230-f04-verdict/make_results.py --candidates /tmp/postmerge-230/live2/candidates --pods /tmp/postmerge-230/live2/pods --c5-summary /tmp/postmerge-230/c5-summary/summary.json` | Exit 0; assembled only F04. |
| The same command with `--check` | Exit 0; exact result bytes match. |
| Temporary candidate copy with one LF appended to F04-C0.json, then `--check` | Subprocess exit 1 with the expected pinned SHA-256 rejection. |
| Temporary copy of pinned source inputs with one LF appended to expected.yaml, then imported `build` | Expected ValueError at the expected-outcome hash; the real source and input files were untouched. |
| Scripted comparison of five candidates, five receipts and 43 F04 artifact-member byte strings against their ZIP members | Passed; 25 repeat projections retained; three summary sentences; ASCII and Python syntax checks passed. |
| `git merge-base --is-ancestor` for measured C5 commit and live source; `git diff --exit-code` across all six configured targets, C5 configuration and workflow | Both exited 0. |
| `npm run check:docs` | Exit 0; 8 inherited warnings in unchanged files, no issue in the new report or receipt. |
| `git diff --cached --check` and staged-path set check | Passed; exactly four authored F04 publication files. |

The final candidate-derived result SHA-256 is `9a848a9e36939d054d75e1840f3ecb2282b492e6566eaf86b8c5116b2f07a2d6`. The generator SHA-256 at these checks is `98b442c118c9c1adb408c25bcc2a8a93c2b37ae28c760d8fae42a9dadafb12cf`. Root also checked every literal Markdown outcome row against the candidate projections through the read-only evidence audit. No runtime source or test assertion changed; local verification was scoped to the publication assembler, evidence and documentation. The PR's normal CI remains separate from the already passing live-source C5 publication gate.

Warning disposition under AGENTS.md rule 8: warnings in files outside this PR are inherited and left unchanged. The final containing head's CI and last cold-review round belong in the PR description; this receipt need not describe its own containing commit.

## Cold review and remaining approval

Round 1 applied repo-review in a fresh high-effort context to the complete authored diff and this receipt. It independently reproduced the assembler's check, verified all 43 pinned artifact members and read the completed batch/stop logs through the GitHub connector. It reported one mandatory P0 covering both new result files because the diff cannot show a verdict label, with no other finding. Its exact result was `Findings: 1 at P0 or P1`; no pre-existing problem was listed.

Disposition: retain both result files under AGENTS.md "Before opening a pull request" rule 8's verdict-step exception, disclose the finding, label the PR verdict, and leave approval and merge to Hari. No P0/P1 is disputed and no P2 was reported. A fresh review of the final diff follows this receipt update; the final round is recorded in the PR description so there is no receipt-only self-reference commit. No verdict is approved by this receipt.
