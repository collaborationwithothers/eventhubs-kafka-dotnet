# C5 timeout recovery and paired acceptance

Revision R2. Main base `2158a93bff0cf8b0fcd1de9ae13f776291342b19`.

## Goal and authority

Complete issue #494 with a qualifying C5 layout and the approved paired main campaign. The previous diagnostic authenticated 40 completed receipts and preserved three receipts from jobs that reached the 2,650-second command limit while tests were progressing. The owner explicitly approved continuing after that attempt. This recovery has its own measured scope, while the product plan and the 324-job, 60% score and 3,000-second paired gates stay in force. [Product plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md) · [Paired acceptance decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7001549347) · [Capacity decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7060787313) · [Measured recovery evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7076127650)

## Scope

| Pull request | Allowed files and purpose | Estimate |
| --- | --- | --- |
| Recovery diagnostic pin | `.github/workflows/verifier-c5-sizing.yml`, its byte-identical `docs/operations/workflows/verifier-c5-sizing.yml.txt` mirror, and `docs/operations/verifier-c5-sizing.md`. Pin the exact recovery source and 18 physical selectors below. | Three files and about 180 to 300 changed lines, based on replacing the two 43-row matrices and updating the runbook. |
| Qualify the recovered layout | `tests/Parchwell.Verifier.Tests/Mutation/{c5_workflow.py,test_c5_workflow.py,c5_runtime.py,test_c5_runtime.py,c5-runtime-qualification.json}` and `docs/operations/verifier-mutation.md`. Integrate the reviewed source and publish validated timing evidence. | Six files and about 850 to 1,500 changed lines, based on the cut fixture, bound records, provenance implementation and tests. |

Publish the exact measurement source `4275b634f14c1f81fcfe91dfcd7deb260a0c8a7f` on `refs/heads/codex/494-c5-recovery-source-wip`. Verify the remote branch tip and GitHub commit API both resolve to that exact source before publishing the pin and again before dispatch. [Source backup](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/tree/codex/494-c5-recovery-source-wip) It changes three cut tables and the corresponding frozen fixture from `3c65a2fa52f16378c3815159cd07f8237e3346bf`, the prior measured source. Keep all application and C# test sources, native Stryker settings, thresholds, worker execution, static cohorts, public mutation matrices, product configuration, scientific outcomes and deployment outside the changes.

## External contracts and invariants

- `C5 hosted timing diagnostic` remains manual on `main`. Set the existing `source_sha` input to the exact recovery source above and enforce it before checkout. Verify Git revision and each row's target SHA-256. Keep the `ubuntu-24.04` runners, 2,650-second inner cap, 2,700-second outer cap including termination grace, 50-minute job limit and original artifact names and retention. The workflow and its mirror remain byte identical.
- The qualification remains schema 2 with its existing CLI, JSON fields and estimate calculation. The final public pair keeps 178 coordinator and 146 companion physical jobs with disjoint names, the complete native and isolated populations, and the existing break at 60%. A hosted summary must report `measurement_complete: true` and `break_passed: true`. The unchanged terminal verifier must report `status: met` and `elapsed_seconds <= 3000` from the exact hosted manifest, summary and run attempts.

## Requirements

1. WHEN pinning the job matrix, THEN compare all 324 generated configurations for the original, prior and recovery trees. The approved recovery changes exactly the 18 configurations listed below versus the prior tree. The 25 retained prior-run configurations match byte for byte. The 281 existing historical configurations, executable runner and settings, cohort declarations, dependency and test inputs, target bytes and role matrices compare equal to their preserved inputs. An unexpected difference blocks dispatch.
2. WHEN a fresh job runs, THEN authenticate its workflow, run, attempt, job, measuring commit and tree, target hash, selector digest, artifact ID/digest/size, report and isolation logs, full native identities and owner, observed workers, and timed completion. Require every one of the 18 jobs to succeed and each receipt to conform to the existing elapsed and rounded-estimate limits. Missing, stale, timed-out and incomplete artifacts remain evidence of the attempt. They cannot qualify a selector.
3. WHEN reusing a prior success, THEN accept only the 25 named unchanged selections from the exact first attempt of run `44995993132`. Verify each selected job succeeded and its original artifact, input maps, native identity inventory, owner, target and physical configuration match the recovery tree. The overall prior failure does not invalidate independent successful shard receipts. None of the three failed jobs, changed neighbors or crossing configurations may use old timing. Keep the original measurement source for each receipt. Preserve the original context source and each retained historical item, timing proof and assessment.
4. WHEN evaluating the qualification in hosted CI, THEN require 324 owners with disjoint sets of 281 historical, 25 retained prior and 18 fresh recovery proofs. For the two measurement sources independently reconstruct the complete compatible maps and exact physical invocations. Recognize only the declared cut transition at the exact current qualification tree, with every cohort and worker input preserved. Keep every existing threshold, timing arithmetic and provenance assertion and prove the additional path fails closed when evidence or scope changes. The final frozen cut fixture uses the successor arrays and retains its existing shape, ownership and cohort assertions.
5. WHEN the protected qualification merges, THEN verify the exact main commit and archive transport fix before launching the one paired C5 campaign. Retain the official hosted summary and the terminal verifier outcome. Close #494 only if the complete exact hosted pair passes both the 60% score and the 3,000-second elapsed gate.

## Exact geometry and measurement scope

The listed boundaries are the interior UTF-16 offsets to replace in the named windows of the prior source. All other boundaries remain fixed.

| Target and SHA-256 | Changed selectors | Replacement cuts |
| --- | --- | --- |
| `Judge/B10FinalJudge.cs` `e37f0604e2483e2c54f73006dca1e6fc68f5ee5e422a660ecd18f94f0240967a` | `b10-final-judge` p1 through p4 and crossing | 3948, 6323, 10104 |
| `Common/Phase/B10FinalProjectionCodec.cs` `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` | `b10-final-projection-codec` p5 through p9 and crossing | 5039, 6477, 8544, 9715 |
| `Common/Results/VerifierResultCodec.cs` `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` | `verifier-result-codec` p18 through p23 and crossing | 43029, 45907, 48370, 50624, 52964 |

Retained prior successes are judge p5 through p12, projection codec p4 and p10 through p15, and result codec p8 through p14 and p24 through p26. Full native reference inventories contain 743, 761 and 1,701 identities respectively. Every node retains one primary or crossing owner under the existing assignment rule. Static cohort configurations are unchanged.

## Test map

| Requirement | Check | Inputs and expected result |
| --- | --- | --- |
| 1 | Scope and source audit | Compare the named committed trees and all generated configs. Exactly 18 changes versus the 43-selection prior tree, including the three crossing complements. Exactly 25 unchanged former diagnostic selections and 281 unchanged original ones. Primary cuts sort strictly, stay inside source and avoid static declarations. A node ending at a boundary is left, one starting there is right, and one strictly spanning it is crossing-owned. |
| 2 | Authenticated hosted evidence and timing link | One exact artifact per expected shard on the one protected main dispatch. Compare identity, pins, complete native geometry and ownership. With otherwise valid evidence, positive elapsed values through 2,650 are accepted at the command limit and 2,651 fails. A rounded estimate at 2,700 is permitted by the existing numeric gate and 2,701 fails. Any incomplete execution, wrong digest, wrong source, missing receipt or out-of-scope artifact fails. |
| 3 | Receipt reuse and prior-source proof | Exact successful R3 job and equal configuration accepts the original R3 measurement source for its retained selector. A prior source on a changed or previously failed shard, a substituted source on a retained shard, changed scope/map/worker inputs, or mismatched artifact/ledger association fails. Original historical item and proof associations compare equal. |
| 4 | Exact qualification transition | True for the reviewed recovery tree and exact map binding. False for cut, cohort, source, worker, config and matrix drift or absent source ancestry. Ensure altered old proofs remain rejected even if the complete numeric ledger would fit the budget. Keep existing timing, topology and estimator tests. |
| 5 | Hosted CI and terminal pair | Run the final-head hosted code/security reviews and required CI. Both exact main workflow runs and the official aggregate must succeed. At 3,000 seconds the pair can pass and at 3,001 it fails. Missing evidence and a score below the existing 60% break each block closure. |

If a new check makes an existing valid fixture invalid, correct the fixture while preserving its assertion and boundary. Preserve deliberately invalid input and assert the exact new failure. Record each fixture edit and forcing check in the PR. The cold review and Codex review check each recorded fixture change and its forcing check. Use hosted CI for checks it covers, including documentation and lint. The exact provider-artifact and cross-tree comparisons exercise data ordinary CI cannot generate. [Verification direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894)

## Runtime basis and deployment assumptions

The timed-out hosts kept both workers active while they hit their command limits. The replay takes each mutant's larger observed duration across the old paired run and the latest diagnostic. For tests unfinished at the cutoff it estimates duration using the failed job's observed 80th-percentile ratio for matched tests. It charges each family's largest completed primary remainder after removing half its observed work. The crossing estimate uses its own observed remainder. A discrete search over recorded node boundaries and interior points minimizes the worst modeled time within each fixed window. On November 22 it projects maxima of 2,414.09 seconds for judge, 2,432.68 for projection codec and 2,454.45 for result codec. Those values estimate two-worker work and fixed time. They neither simulate worker scheduling nor guarantee hosted wall time. Native tests run under load on fresh hosted runners. The fresh receipts determine qualification, and the genuine paired run measures wall time and aggregation with both complete input sets. No application or infrastructure deployment is part of recovery. [Exact minimized replay evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7076191819) · [Prior diagnostic](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44995993132) · [Earlier paired run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44928264518)

## Acceptance and bounds

- Before dispatch require the independent plan and selector check and the protected sizing-pin merge. Dispatch `verifier-c5-sizing.yml` on exact `main` once with the reviewed input. At most 18 new hosted jobs and their receipts, plus intake of at most 25 authenticated retained receipts and comparison of all 324 configurations. On exhaustion, preserve evidence, push -wip and hand off to the next session. No automatic retry.
- Assemble only complete verified evidence, then require hosted `c5_runtime.py check`, the required final-head CI and both hosted reviews, and the protected qualification merge.
- After merge dispatch one paired attempt of 324 physical jobs. Validate through the existing terminal gate. If that bound is exhausted, preserve evidence, push -wip and hand off to the next session. Missing evidence, failed checks, unapplied authorized scope or time/score failure block closure.

## Independent plan check questions

1. Runtime-dependent work is bounded to 18 new receipts, 25 retained receipts and one exact 324-job pair. The exhaustion action is recorded above.
2. Existing job counts, complete population, score and wall-time gates stay fixed. Exact two-source evidence and narrower recovery prevent stale timings from qualifying this layout.
3. The named recovery Git object and the prior raw reports support the offline check. The verified remote backup ref makes the source fetchable. The protected pin binds its exact SHA and verifies checkout before measurement. The authorized hosted diagnostic and paired run establish fresh-runner performance and complete aggregation.

## Open questions for Hari

None.

## Stop rule

Preserve failed or incomplete evidence. Follow the bounded handoff if an attempt is exhausted or a contract or scope change becomes necessary. Only the repository owner merges protected paths. [Repository rules](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2158a93bff0cf8b0fcd1de9ae13f776291342b19/AGENTS.md#handoffs)
