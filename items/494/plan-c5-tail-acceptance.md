# C5 tail acceptance

Revision R3. Main base `b31928a2ed22a634e8e1a66edb953311eaa9ca87`.

R2 spells out the exhaustion action for the fixed diagnostic, evidence intake and paired attempt. The candidate and checks are the same as R1.

R3 applies the independent check’s wording about observed primary and crossing overhead. The candidate, requirements and limits are the same as R2. [Applied R2 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7067238699)

## Goal and authority

Complete the paired C5 acceptance required for #494 after a complete main campaign within 3,000 seconds. Both failed campaign legs measured the base above and all 324 mutation jobs succeeded. The official aggregate missed companion receipts because its action omitted the final page, and the pair exceeded the wall limit. Repair the download through #580 before the next acceptance. This plan covers the separate repair to the slowest mutation shards. [Product plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md#mutation-and-acceptance-route) · [Paired acceptance](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7001549347) · [324-job authorization and standing direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7060787313) · [Measured failure](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7067076568)

## Scope

| Pull request | Files and purpose | Estimate |
| --- | --- | --- |
| Timing diagnostic pin | `.github/workflows/verifier-c5-sizing.yml`, its byte-for-byte `docs/operations/workflows/verifier-c5-sizing.yml.txt` mirror and `docs/operations/verifier-c5-sizing.md`. Pin the exact reviewed measurement source and 43 physical selectors below. This PR can merge without final estimates. | Three files and about 150–210 changed lines, counted from two 43-row matrices replacing the existing selector lists and a short runbook edit. |
| Timing qualification and selector correction | `tests/Parchwell.Verifier.Tests/Mutation/{c5_workflow.py,test_c5_workflow.py,c5_runtime.py,test_c5_runtime.py,c5-runtime-qualification.json}` and `docs/operations/verifier-mutation.md`. Publish the candidate cuts and evidence only after the diagnostic is verified. | Six files and about 850–1,400 changed lines, based on the selector diff, per-shard evidence and record edits, and qualification logic/tests. |

Keep application and C# test sources, product configuration, Kafka/Azure, scientific outcomes, native Stryker configuration, workflow triggers, both mutation matrices and the existing aggregation/terminal gates out of this change. The exact reviewed sizing source contains only the two selector-table edits. It can be retained as a backup reference before the sizing pin merges. The final PR integrates it with current `main`, with #580 merged, and proves exact mutation inputs and selectors again.

## External contracts

- The existing `C5 hosted timing diagnostic` remains manual on `main` and accepts only the reviewed `source_sha` input value `3c65a2fa52f16378c3815159cd07f8237e3346bf`. The checkout commit and each `target_sha256` are checked. Jobs stay `ubuntu-24.04` with the existing 2,650-second inner limit, 2,700-second outer bound including termination grace, 50-minute job limit and original named artifacts and retention.
- The public pair retains 178 coordinator jobs and 146 companion jobs, the same 324 physical selector names and targets, the existing `thresholds.break: 60` and full native and isolated inventories. A valid terminal summary has `measurement_complete: true` and `break_passed: true`. `c5_campaign.py terminal` must return `status: met` and `elapsed_seconds <= 3000` on exact terminal runs and the hosted aggregate artifact.
- Qualification remains schema 2 with its existing JSON fields, command interface and numeric calculation. New/old proof sources keep their actual commits and artifact digests. There is no public API, message contract or Azure deployment change.

## Enforced requirements

1. WHEN selecting the measurement workload, THEN use the three literal UTF-16 cut arrays and exact source below. Prove the generated 43 changed physical selections match the diagnostic matrix precisely, including the three crossing selections, and prove the other 281 generated selection configurations equal the last accepted qualification's. Compare both mutation matrices, source hashes, executable runner fingerprint, cohort declarations, test inputs and Stryker configuration. An unexpected difference blocks dispatch.
2. WHEN verifying the diagnostic, THEN authenticate the known workflow/run/attempt/job matrix, SHA and tree, target SHA, effective selector SHA, authoritative artifact ID/digest/size, report hash, executable completion, native identity inventory, complete required isolation, observed worker coverage, and elapsed and work counters. The measured command and rounded estimate must meet their existing 2,650-second and 2,700-second limits respectively. Require all 43 receipts. No value from the planning model is a measured estimate. Retain incomplete and failed evidence.
3. WHEN retaining a timing on the candidate, THEN first prove the new tree equals the pinned measurement tree in executable runner, relevant shared and target inputs, complete configuration and both matrices. Compare candidate and prior reviewed trees and permit exactly the listed cut changes. Use exact effective per-shard ownership and span, with every cut included for crossing selectors and all static cohort data pinned. Delegate unchanged selectors to the previous reviewed proof only after that comparison. Preserve their original committed item, proof, source and evidence associations. Do this for previous classifier, codec and historical proofs as applicable. Never roll a changed selector backward to validate its old evidence.
4. WHEN qualifying any of the 43 changed selections, THEN require new, ordinary measured evidence from the pinned measurement source with the exact candidate selection. The schema-2 map binding already exists under the earlier classifier source. Reconstruct the candidate source's complete maps and compare them with that same context before accepting its additional measured source. Keep the context's old source for existing receipts. Treat the prior same-source pinned codec and judge records as replaced only for these exact changed invocations with fresh measured receipts. No arbitrary alternate source, estimate or special-case cut transition qualifies.
5. WHEN validating in hosted CI, THEN keep every existing runtime, cohort, projection and workflow assertion. Update the frozen expected cut fixture to the literal cut arrays and leave its shape, inventory and ownership checks in place. Preserve required hosted CI, code/security review and the protected owner merge. Run once on one exact main commit after both repairs merge and apply the untouched terminal check to the genuine paired hosted artifact. Only a successful terminal check permits closing #494.

## Exact candidate and inventory

| Target and source SHA-256 | UTF-16 cut positions | Changed selectors |
| --- | --- | --- |
| `Common/Results/VerifierResultCodec.cs` `8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba` | 476, 807, 1133, 1376, 4456, 8349, 11870, 15788, 19401, 23344, 25714, 28131, 30037, 31698, 33675, 36075, 39474, 42671, 45351, 48143, 49967, 52194, 56532, 61904, 65532 | `verifier-result-codec` p8–p14, p18–p26, crossing (17) |
| `Judge/B10FinalJudge.cs` `e37f0604e2483e2c54f73006dca1e6fc68f5ee5e422a660ecd18f94f0240967a` | 4854, 7814, 10370, 12877, 15548, 17395, 20266, 23166, 24193, 25909, 27524 | `b10-final-judge` p1–p12, crossing (13) |
| `Common/Phase/B10FinalProjectionCodec.cs` `43b65bb7b91010a7bf1b97343ce8c134c686ea9c2d4d973f8e284fa4bcfdcf4f` | 1109, 1747, 2198, 4005, 5839, 7179, 8842, 10089, 11342, 12368, 13301, 14683, 16156, 18344 | `b10-final-projection-codec` p4–p15, crossing (13) |

The exact-hosted reports contain 1,701, 743 and 761 unique native identities respectively. Half-open primary geometry and the crossing complement assign each one once. Same-count redistribution leaves cohort definitions and the primary cuts that bound them unchanged. Candidate placement accounts for full node spans, including nodes that span one or more cuts. Native inventory, scoring populations, verdicts, and static isolation rules stay fixed.

## Test map

| Requirement | Check | Inputs and expected result |
| --- | --- | --- |
| 1 | Candidate geometry audit and hosted frozen cut/ownership tests | Compare both pinned trees and all 324 generated configs. Exactly 43 differences, 281 equal configs, 178/146 disjoint physical owner maps and identical cohort data. The three raw native inventories produce 49, 46 and 33 crossing geometries. A span ending at a cut belongs to the left primary, one starting there belongs to the right, and one strictly spanning it belongs only to crossing. A changed count, duplicate owner, empty or misordered span, escaping cut, or cut through a static declaration fails the corresponding shape or equivalence proof. |
| 2 | Authenticated diagnostic intake | For each expected artifact use one instance, exact run/attempt/shard digest and exact source, report and selector digests. A missing receipt, incomplete command, unsupported worker count, duplicated identity, mismatched provenance or artifact, or an estimate of 2,701 seconds fails. A correctly derived 2,700-second estimate can pass the numeric ceiling, subject to all other checks. Preserve earlier evidence. |
| 3 | Retained proof tests | An unchanged primary in each affected target, a retained static cohort and an unaffected historical measurement must still qualify from the original record. A changed span, changed crossing complement or different dependency or matrix fails the retention proof. A changed per-shard estimate or lost source/artifact association fails. Keep absence, exact boundary equality and mismatched projection/code coverage. |
| 4 | Fresh proof tests | The candidate's new measured receipt can share the exact older context maps only from the pinned measurement source and its exact generated selector. An old receipt for a changed selector, other candidate or old source, arbitrary additional cuts, mismatched target, incorrect evidence hash or locator, missing measured receipt, malformed proof or out-of-bound estimate fails. Exercise one each of changed judge and projection pinned selectors alongside an unpinned case. Other work retains its existing proof rules. |
| 5 | Hosted tests and paired acceptance | Run the existing hosted required checks. Confirm actual aggregate requires all 324 receipts and preserves break 60, then use exact terminal workflow/run/attempt evidence. At 3,000 seconds the timing gate can pass and at 3,001 it fails. Incomplete evidence, a failed job or score failure prevents acceptance. Existing CI tests cover this terminal boundary. |

If a new check rejects an existing fixture, repair that fixture while retaining its assertion and the boundary it exercises. A deliberately invalid fixture keeps the invalid input and asserts the exact new rejection. List each such change and forcing check in the PR. The workflow and cut fixtures keep every existing assertion. No C# or CI verification suite is repeated locally under the [hosted-check direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894). Comparative evidence replay and full committed selector auditing are separate from hosted test duplication.

## Runtime basis and deployment assumptions

The exact primary and crossing reports and command logs came from the loaded, standard hosted jobs in the failed paired workflow. The model sums two-worker per-mutant durations at their full spans. For primary shards it adds the greatest family primary elapsed time less half its observed mutant work. For crossing it adds the corresponding crossing value and assigns crossing work to its own shard. It projects maxima for the reviewed candidate of 2,121.0, 2,182.8 and 2,142.1 seconds for result codec, judge and projection codec respectively, at the observed work rate. A sensitivity calculation inflating measured work by 30% projects 2,673.1, 2,771.6 and 2,716.6 seconds respectively. These are estimates from the November 21 run, without launch or final aggregation time, and cannot establish that a particular hosted runner meets a bound. The latest pair added 105 seconds for its aggregate job, with only part of the companion input. Test the diagnostic selection on fresh hosted runner allocations under the existing limits. The subsequent genuine paired run measures all overhead under load with all artifacts. No deployment proceeds on the modeled result alone. [Result codec p21](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44928264518/job/136919052914) · [Result codec p24](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44928264518/job/136919052876) · [Aggregate](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44928264518/job/136934088935)

## Acceptance and bounds

- Before dispatch, require exact preflight under requirement 1 and reviewed protected merge of the sizing pin. Dispatch `verifier-c5-sizing.yml` on `main` once with the pinned source. At most 43 jobs and one intake of 43 receipts. On exhaustion, preserve the evidence, push -wip and hand off to the next session under AGENTS.md rule 11. No automatic retry.
- Publish qualification only after all source, geometry, artifact, receipt and arithmetic checks pass on the pinned diagnostic. Hosted CI invokes `c5_runtime.py check` on the final PR head with the committed qualification. Protected merge and final-head review remain required.
- After both repairs merge, compare exact `main` against the pinned candidate and accepted action fix. Dispatch one paired main attempt under the existing campaign rules. Use `c5_campaign.py terminal` with the measured SHA, hosted manifest, hosted summary and both exact run attempts. Retain the definitive result and close #494 only once every existing issue gate is satisfied. If the one-attempt bound is exhausted without acceptance, preserve the evidence, push -wip and hand off to the next session under AGENTS.md rule 11.

## Sources

Pinned bases above, and `c5_workflow.py`, `c5_runtime.py`, `c5_campaign.py`, `verifier-c5-sizing.yml`, `verifier-c5-mutation.yml`, `verifier-c5-mutation-companion.yml`, and `docs/operations/verifier-mutation.md` at the stated main base. The issue comment links the provider run and all required authority. [Original spec and amendment history](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494) · [Companion action repair](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/580)

## Independent plan check questions

1. The only new workload is the fixed 43-job diagnostic and its 43 receipts, followed by one paired 324-job attempt. No work count is discovered at runtime. Exhaustion preserves evidence and blocks the associated gate. The escalation action for both limits is specified under Acceptance and bounds.
2. The accepted mutation requirements, score and 3,000-second limit stay the same. The diagnostic scope is exactly the changed configurations from an observed failure and its exposed late neighbors.
3. The source-pinned candidate is available as a local Git object for this plan check and must be reachable before dispatch. Provider inventory and job metadata and the native reports are from the cited pair. The later hosted transport fix and fresh runner performance are exercised at the designated steps. Candidate timing estimates are not treated as qualification evidence.

## Open questions for Hari

None.

## Stop rule

If a dependency, proof, diagnostic or terminal attempt fails, preserve its evidence and diagnose within the fixed data and existing limits. Do not rerun automatically, publish a provisional acceptance, invent a receipt or change qualification or the pair's limit. If resolution changes a contract, requirement or scope, save the work and follow AGENTS.md GOVERNANCE “Handoffs”. Protected PRs wait for the repository owner to merge.
