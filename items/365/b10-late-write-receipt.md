# B10 phase late-write implementation receipt

This change implements the [approved Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6652574041) with all three [plan-check corrections](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6652678919). It completes synthetic verifier finalization under [ADR-021](../../docs/blueprint.md#adr-021-b10-accepts-timed-out-sends-application-retries-and-one-producer-restart-in-a-second-job-and-classifies-late-appends-up-to-a-sweep-line); no live result is published.

## Execution card

- Deliverable and current authorization: approved plan Exact changes sections 1-9 and the linked standing-approval plan check. Source commit: `8491e3a324578cf33ddcb78aa12521946be04883`.
- Mandatory gates: approved plan Acceptance commands and evidence; local cold review under [pull-request rules](../../docs/agents/pull-requests.md), final-head CI, hosted Codex review and adopted separate Claude review. Hari merges protected paths. Hosted mutation acceptance remains postmerge main-only evidence work.
- Exact verification commands: `./scripts/verify-dotnet.sh`; `python3 tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`; `npm run check:docs`; `git diff --check`. Also script-compute classification counts and target/shard totals, and compare the mutation trigger object with pinned main.
- Earliest authorized publication checkpoint: tested authored diff with applicable cold-review coverage, after authority refresh. No push precedes that checkpoint.
- Review budget and exhaustion action: [pull-request rules, rule 8](../../docs/agents/pull-requests.md#rules), including its cumulative budget and handoff requirements, apply without reset. Cumulative review usage is recorded in the cold-review record below.
- Execution caps: approved plan Acceptance commands and evidence sets 12 distinct verification root-cause groups, two focused reproductions per group, and 20 changed-file documentation-warning dispositions. Exhaustion action: `push -wip and hand off to the next session`.

## Verification and predicate map

Implementation and local acceptance are complete. The predicate map below covers the approved finite regression tables; measured command outcomes are recorded below.

## Predicate-to-test map

All fixtures are synthetic. Boundary cases keep the other inputs valid; the original repeat result stays nonfinal and count-free.

| Predicate | Regression evidence |
| --- | --- |
| Closed phase note/finalization schemas, fixed paths and source/role identity | `B10SweepEvidenceTests`, `PhaseWireCodecTests`: missing, duplicate and unknown members; root/partition/reference/count types; C0/C1, exit/status and role substitution. |
| Consumer-owned timing and parser bounds | `B10SweepEvidenceTests`: capture, verifier completion and worker-start comparisons immediately below/at/above their boundary; settle 999/1000/1001 with independent valid elapsed time; invalid settle and overflow; input/output bytes and partition count. The committed note-depth test rejects limit 2 and accepts limits 3 and 4. It does not exercise finalization depth or added nesting; no such regression coverage is claimed. |
| Eligibility authenticates completed original checks without provisional counts | `VerifierResultCodecTests`, `B10RepeatVerifyTests`: each true-flag prerequisite removed independently; omitted legacy flag remains null, nonboolean and non-B10 flag rejected; permitted timeout/retry pair is eligible, guard/retry/publication failures are not. |
| Only validated earlier same-phase ownership excludes original-window records | `B10EarlierRepeatExclusionTests`: both earlier roles excluded; own role retained; later/unknown/other-phase/missing/duplicate headers fail; missing/source/job/predecessor/path/duplicate ownership fails; repeat 1 performs no earlier storage read. |
| Shared registration and raw-consumption budgets | `B10EarlierRepeatExclusionTests`: byte and entry budgets below/exact/above complete earlier listing; raw record and byte budgets include excluded earlier records; incomplete and over-limit listings fail. |
| Validated monotone vectors and half-open original/late windows | `B10LateWriteChecksTests`, `B10SweepHandlerTests`: invalid/empty/duplicate vectors; preserved old partitions, new partition zero starts; start/end/sweep/current boundaries, excluded endpoints and post-sweep observations; empty and failed drains. |
| Composite own-ledger joins and union classification | `B10LateWriteChecksTests`: literal six-count fixture with/without late rows, loss-to-OK and failure-to-phantom changes, mismatched key/sequence, missing/duplicate joins, repeated Kafka coordinates, distinct-offset copies and cross-instance IDs. |
| Phase-wide sweep execution and publication | `B10SweepHandlerTests`: authenticated original result/receipt/start/inventory and both completions/ledgers; shared budgets, failed scan invalidity, original immutability, cleanup before create-only finalization and completion-last behavior. |
| Sweep authentication and original trust provenance at judge | `B10SweepChecksTests`, `JudgeHandlerTests`: phase/source/Job/path/hash/length/exit/eligibility substitutions; note timing; stale original bytes; missing/failed sweep invalidity; invalid individual repeat with completed scan; legacy originals cannot gain final counts. |
| J1-J6, original invalidity, incompleteness and checked sums | `PhaseJudgeTests`, `JudgeHandlerTests`: each independent check survives accepted finalization, original known-invalid provenance remains separate, pure intake-only behavior remains incomplete, aggregate overflow suppresses totals and first-producer-only metrics remain absent. |
| Candidate finality/count/status consistency | `PhaseCandidateCodecTests`: true/false/omitted markers, count/status/sweep combinations, legacy count-free nonfinal B10 reads, non-B10 rejection and residual-risk text. |
| Mode, options and composition | `B10SweepOptionsTests`: exact spelling; required scalar and duration budgets absent/negative/zero/positive; forbidden repeat fields; expected Job; settle/process-time bounds; setup retention; sweep handler without scenario services and blob-only B10 judge. |
| Mutation target/matrix/aggregation integrity | `test_c5_workflow.py`: all existing targets preserved, five additions unsplit, complete-below-threshold fixture uses survived populations, threshold/projection and aggregation regressions retained. |

## Verification dispositions

The first development compile found a shadowed local receipt variable; it was renamed. An overlapping development build encountered the not-yet-written sweep handler; the coordinated build waits for that file. Analyzer feedback required static test arrays, which were hoisted without changing assertions. The documentation command initially lacked the repository's npm dependencies; `npm ci --ignore-scripts` installs the locked toolchain before rerunning it. The synthetic C1 fixture also needed its reused A01 baseline recipe to admit C1 before constructor-time identity initialization. New tests now expect the strict registration codec and native cleanup exception types exactly. Publication-unavailable fixtures clear eligibility when removing a snapshot, preserving their existing independent J2/J3 assertions. The input-byte fixture includes registration listing name bytes. These fixes preserve all existing validity assertions.


The disposition register groups development failures by their cause. No group used more than two focused reproductions; acceptance reruns follow the changed source or mandatory gate.

| Group | Cause and disposition | Focused reproductions |
| --- | --- | --- |
| 1 | Integration compilation: renamed a shadowed receipt local; froze source before rebuilding to remove mixed-snapshot observations. | 1 |
| 2 | Documentation tools absent: installed the locked npm dependencies and reran documentation checks. | 1 |
| 3 | CA1861 in new tests: hoisted immutable test arrays. | 1 |
| 4 | Test runner invocation mismatch: used the repository's Microsoft Testing Platform invocation. | 1 |
| 5 | Existing watchdog startup cancellation under overlapping work: passed in the frozen-source acceptance run. | 0 |
| 6 | C1 synthetic fixture lacked the selected baseline configuration: added the scoped constructor-time synthetic alias. | 1 |
| 7 | New tests assumed the wrong exception type: asserted exact strict-codec and cleanup-wrapper types, including the cleanup inner exception. | 2 |
| 8 | Existing publication mutation left new eligibility structurally inconsistent: cleared eligibility with the removed snapshot, preserving J2/J3 assertions. | 0 |
| 9 | Cold Docker creation exceeded the component budget: removed the exact abandoned container; isolated phantom test passed without code/configuration changes. | 1 |
| 10 | Input-boundary fixture omitted listing name bytes: included those charged bytes; all host sweep tests passed. | 1 |
| 11 | New async test calls omitted the xUnit cancellation token: passed the test context token. | 1 |

## Commands and measured results

Source: pinned `8491e3a324578cf33ddcb78aa12521946be04883` plus this authored diff. No generated reports or logs are committed.

| Command or check | Result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Exit 0 with SDK 10.0.402: locked restore, Release build/analyzers, architecture, mandatory suites, nonempty XML reports and warning-as-error control all passed. |
| `python3 tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py` | Exit 0; 78 tests passed. |
| `npm run check:docs` | Exit 0 after new files were included in the index; 8 warnings in unchanged files, none in changed files. |
| `git diff --check` | Exit 0. |
| Scripted mutation scope and trigger comparison | 17 targets and 28 shards; five appended target files exist; trigger object equals pinned main; no committed workflow copy exists. |
| Independent literal classification script | Original counts `(3,0,0,2,0,0)`; original-plus-late counts `(3,0,2,0,2,2)`. |
| Focused B10 acceptance checks | 99 judge acceptance cases passed; all 65 sweep host cases passed. The broader B10 run passed the remaining 1230 cases before the host fixture corrections. |

The initial complete verifier run measured 6728 tests, 6720 passing and 8 failing. It exposed the corrected new-fixture cases above, one stale compiler snapshot, an existing watchdog startup race during overlapping build work, and a Docker container creation timeout. The isolated phantom component reproduction passed after the exact abandoned, unstarted container was removed; no production code or environment configuration was changed for it. The exact acceptance run passed on the complete frozen source, including the mandatory architecture, reports and warning control gates. It measured 15 architecture, 56 enrichment, 486 Kafka, 1180 load generator, 336 scenarios and 6801 verifier tests passing, with no skipped cases. Its transient reports are in `/tmp/parchwell-dotnet-verification.XX1J95fL`; they are not committed.

Changed-file documentation-warning dispositions used: 0 of the approved cap of 20. The unchanged-file warnings receive the required blanket disposition in the PR description. No local mutation campaign, Azure run or scientific result publication was performed.

The full .NET acceptance above describes the implementation before the later J1 regression addition. After cold-review round 2, `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --minimum-expected-tests 1 -- --filter-class '*PhaseJudgeTests'` rebuilt the affected project and passed the complete affected class; the new slot-only case passes. No production code changed after full acceptance.

## Cold-review record

Round 1 comprehensively reviewed staged tree `c9c26456e4d9a2e0638d84dbbcc4d7887b305849` against source `8491e3a324578cf33ddcb78aa12521946be04883`. Its complete report found one P1: the parser-depth row overstated the measured depth and committed coverage. Disposition: corrected that row to describe exactly the committed note-depth test, without claiming finalization-depth or added-nesting coverage. No code or test changed. The full report is retained for the PR self-review record, outside this receipt.

Round 2 comprehensively reviewed staged tree `adbae66272e09446fe797225f67a090d88eb0571` against the same source. Its complete report found one P1: the accepted-finalization matrix omitted independent J1 slot failure. Disposition: add only `SlotPresent=false` to an otherwise accepted-finalization case and retain the existing status, finality and null-count assertions, plus an exact J1-diagnostic assertion. No production code or existing assertion was loosened. Complete reports for both comprehensive rounds are retained for the PR self-review record.

Round 3 is a focused correction review of changes since the round-2 tree, the resolved J1 finding and affected test/receipt context. Its focused diff is `/tmp/issue365/review-3-focused.diff`; the full PR diff is `/tmp/issue365/review-3-full.diff`; its tree identifier is `/tmp/issue365/review-3-tree.txt`. Cumulative comprehensive count: 2; cumulative focused count at launch: 1. The complete focused report belongs in the PR self-review record, outside this reviewed receipt. Final-head hosted CI and Codex review remain separate gates.

## Claude review correction

The user requested correction of the [separate Claude review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/388#pullrequestreview-5782779873) on source `85e7424fc9cf8c13326f2d646182fefafccc83d5`. Its P2 identifies duplicated or misleading original-result trust provenance copied from sweep reasons. The judge now reserves `trusted_result_invalid` for its own `KnownInvalid` evidence and deduplicates repeat reasons. The sweep's invalid status and other diagnostics remain intact.

The added `PhaseJudgeTests.Given_AcceptedSweepWithInvalidOriginal_When_Judged_Then_OnlyJudgeTrustControlsProvenance` crosses original known-invalid evidence and receipt acceptance, with all other gates valid. It asserts invalid status, absent counts, false finality, one copy of retained diagnostics and trust provenance only from the judge. Existing assertions are preserved. This is a diagnostic correction within the approved plan-check provenance requirement, with no new scientific gate.

The execution card above remains in force. This correction uses the final focused review round under the existing cumulative budget: two comprehensive and two focused rounds including this round, without reset. Review mode: focused changes since `85e7424fc9cf8c13326f2d646182fefafccc83d5`, with the full PR diff as context. Reviewed tree and diffs are recorded outside the repository under `/tmp/claude388/`. Verification results will be recorded before the frozen review starts.

Correction verification: with SDK 10.0.402, `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --minimum-expected-tests 1 -- --filter-class '*PhaseJudgeTests'` rebuilt the affected test project and passed all 89 cases, with zero failures or skips. `git diff --check` passed. The prior full acceptance remains evidence for unchanged behavior; final-head CI remains mandatory after the correction. Documentation-check outcomes are recorded in the PR self-review record.
