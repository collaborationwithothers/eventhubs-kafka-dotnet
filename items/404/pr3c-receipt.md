# PR3c full classification execution receipt

This change completes diagnostic reconciliation and classification for the abrupt B10 verifier. Its output remains synthetic, nonfinal and inadmissible to scientific readers. Hari merges the protected verifier changes.

## Execution card

- Deliverable and scope: [Revision 3, sections 1 to 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/48c52669fd40a3bb80ca0378e061f1d6153d6a22/items/404/plan-pr3c-full-classification.md).
- Authorization: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6828172387), with recognized decisions [6824437250](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6824437250), [6826128120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6826128120), [6827180600](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827180600), [6827265635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827265635) and [6827960705](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827960705), in creation order.
- Source: main `78cba6e467c7bd0333f131e159f4989b6cfee828`. Before editing, the managed Docker local-socket `info` check passed. A Python SHA-256 assertion over the pinned plan bytes passed: `9a69405cab00ac224539d5c5794c9eae13d1f4df9f7ce28d10cd2fdbda5ea282`.
- Mandatory gates: plan acceptance A1 to A6; full authored-diff cold review; final-head CI and hosted code/security reviews. No mutation target changes, workflow dispatch, trial, scientific results or expected-outcome changes.
- Commands: `./scripts/verify-dotnet.sh`; `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `bash infra/scripts/lifecycle/check-offline.sh`; `npm run check:docs`; scripted scope and frozen-path audit of `git diff --name-only origin/main...HEAD`; scratch live-byte replay of artifact `13945847489` through the evidence readers.
- Earliest publication: after the stated offline acceptance gates and applicable cold review pass, publish the tested PR while CI and hosted review run together. No merge by this session.
- Fix caps: 20 verification-failure root causes and 20 review-finding root causes per implementing session. Either exhaustion means `push -wip and hand off to the next session`. Files and contracts are frozen, not lines.
- Cold review budget: at most two comprehensive passes and two focused correction rounds cumulatively. Exhaustion or unsafe focus saves `-wip` and hands off to Hari under docs/agents/pull-requests.md rule 8.

## Implementation choices and predicate map

The plan's section 6 is the baseline predicate map. The [exact-revision check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827866158) supplies these implementation corrections:

- P2-A: T8 produces original c4 at partition 0, offset 0, avoiding unrelated synthetic padding.
- P2-B: TP1 maps to requirement R1; TP2 maps to requirement R12. These are requirements, not eligibility reasons.
- P2-C: unavailable first/successor evidence skips the specified dependent checks and adds no reason. The contract includes the check's exact sentence, and a missing-first-registration regression preserves the literal problem/reason lists.
- P2-D: the preexisting affected same-instance test has unjoined `warmup` and `s5`; its `s3` joins. No existing assertion was changed before the scope stop.

## Continuation authority

The [test-fixture amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6829647955) resolves the scope stop recorded below. It authorizes only the opaque-inventory expectation, independent depth fixture, and consistent native-padding fixtures it names. The [artifact-source decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6829727998) supplies A6's ZIP.

Before resumed edits, managed local Docker passed again. The pinned plan hash passed again. The supplied ZIP has 76433 bytes and SHA-256 `e26a74e052bb763c4e2d8a6534009469ad19b821635d60d83f0979b807b1d878`, matching the decision's API digest. It remains outside the repository. Verification and review ledgers continue at 1/20 and 0/20; cold budget remains 0 comprehensive and 0 focused completed.

## Implementation choices

- The frozen-test conflict in the first attempt is resolved by the fixture amendment. The revised fixtures preserve independent checks and existing boundaries; the final assertion inventory below records the exact changes.
- New check 19/20 problem details are omitted. The contract requires the problem and reason literals, permits an empty optional details list, and leaves detail text to the implementer. This preserves the preexisting diagnostic-result assertion on that list.
- A raw-read budget charges each actual artifact input read once. A separate shared parsing budget checks rows, entries and parsed byte subsets. Parsing captured evidence already read by the abrupt reader does not charge the raw-read budget twice; output readback is not an input.
- Internal readers and classifiers remain inside `VerifyRun/Abrupt/`. The ordinary judgement and publication files in the C5 mutation target list remain unchanged.
- Trial recognition uses the approved run-ID pattern. Existing scientific readers retain their existing acceptance rules.

## Predicate map and assertion inventory

The pinned plan's section 6 maps requirements to T0 to T26, TP1/TP2 and TC1. TP1 maps to R1 and TP2 to R12. The implementation includes boundary and unavailable-input cases with unrelated evidence valid. Detailed test names and measured results will be recorded after verification.

## Verification ledger

The first attempt's HTTP 403 artifact download is retained as one root. The supplied ZIP resolves that access gate through Hari's artifact-source decision. Subsequent verification roots are installer argument order (corrected), A2 tool PATH (corrected), handler generic task inference, reconciliation constant-array analyzer diagnostics, a stateless test-helper analyzer diagnostic, and MSBuild workers exiting prematurely (cause unverified). The build diagnostics occurred before live-byte replay; no A6 live-byte check has failed. Cumulative count is 7 of 20; review roots remain 0 of 20. Cold rounds started/completed: 0 comprehensive, 0 focused.

No program output is committed. Scratch logs, exports, tools, verified artifact files and the A6 harness live at `/workspace/scratch/404-pr3c/` outside the repository.

## Second scope stop

The exact fixture amendment resolves the three originally identified conflicts. A further existing frozen assertion conflicts with checks 23 and 25: `B10AbruptVerifyCoverageTests.Given_AbsentControlTerminalOrNullableCapturedStatus_When_HostProjectsResends_Then_ObservedFieldsRetainTheirValues`, case `absent-control`. On baseline `78cba6e4`, lines 641 and 649 to 651 remove terminal `t4`, assert no problem, and then project a member's null terminal. The new contract requires problem `b10_abrupt_verify_first_evidence_invalid`, reasons `b10_restart_identity_unauthenticated` and `b10_abrupt_control_incomplete`, and `arms=null`. The amendment permits exactly its named changes and no other frozen assertion changes. This case's assertions and input remain unchanged. Plan section 12 therefore stops this continuation before further implementation.

Recommendation: authorize this subcase to assert that exact problem/reason list and `arms=null`, keeping its removed-terminal input and retaining every assertion in the other three nullable-status/failure subcases. A revised decision or approved plan must resolve this contradiction before continuation.

Already identified continuation work also includes two stale manifest fixtures in the same file: `Given_AcceptedThenRejectedOriginalAndAnotherUnconfirmedOriginal_When_HostReconciles_Then_OnlyUnrejectedOriginalIsSelected` changes the completion invocation predecessor/sequence without updating the successor inventory; `Given_TwoSuccessorInvocationsInOriginalOrReversedOrder_When_HostRuns_Then_OnlyOriginalOrderAuthenticates` adds an invocation and ledger row while leaving the one-entry inventory. Their assertions remain unchanged. The next session must make these fixtures coherent with check 26 within its authorized scope, or return any necessary assertion change for decision.

## Exact test changes and coverage remaining

- Existing T2/T3 cross pair, T4 pair product, T5 same-instance pair, T6/T7 unavailable successor and T14 diagnostic-reader cases gain problem 8 where the old window contains synthetic padding or unjoined attempts. Coverage's independent hash/role mismatch case gains problem 8 for the wrong-payload/instance/attempt records. Boundary's valid two-killed-original case gains problem 8 for padding. Every other count, witness, member and reader assertion remains.
- Closed data/arm/member lists gain only section 3.4 members. The optional `problem_details` empty-list assertion is preserved.
- The plan-listed problem-2 assertions change from null end line/false window completion to a present end line/true completion for the now-required drain; arms and problem assertions remain.
- The explicit fixture amendment changes only opaque-inventory's `[]` to the successor-invalid problem, moves depth nesting to `termination.data.detail` (unread by the enforced checks), and makes boundary padding capture sequence/counts coherent with flags false/false. Existing depth, count and byte-size expectations remain. The dedicated invalid journal-padding test input is preserved.
- New classification, fixture and predicate-boundary files cover the plan's new fields and most named cases. Last child/cgroup/leader boundary additions and the dedicated `PadJournal` input restoration are unbuilt at this stop. The prior test-project build was green before those edits.
- Known unfinished map details: T23 and T26 currently use an empty timeout baseline instead of the literal T2 inputs; T19's old diagnostic-reader case uses padded evidence instead of clean T2 output; check-17 and timeout-listing/parser boundaries need their final map and coverage. No claim is made that all predicates or test rows are complete.

## Remaining gates

A2 passed with 362 tests (`OK`) after correcting the local tools PATH. A3 passed with the pinned tools directory, all chart groups and both smoke renders valid. A6 passed on the exact supplied first/successor bytes: checks 4 to 13 and 21 to 28, including separate first/successor guard checks. The scratch in-memory artifact-store harness performed 18 reads and 2 listings, with no writes or Kafka sessions. Its source and logs are outside the repository at `a6-replay/`. The build reported zero warnings and errors. No byte of the source evidence was changed.

A4 passed with 9 advisory warnings, all in unchanged files; they are covered by one blanket disposition and remain outside this PR. The focused run was canceled on the scope stop (runner exit 58); it reports 16 completed cases with zero failures/errors, but it did not complete the component gate. Its containers and network were cleaned up by the runner. The full `./scripts/verify-dotnet.sh` gate has not run. New boundary edits remain unbuilt, and no cold review has started. This receipt makes no overall completion claim. No PR, hosted review, merge, workflow dispatch, trial, expected outcome or results change has occurred.

Event Hubs acknowledgement-to-watermark visibility remains inferred and not verified by an official source. The accepted trial-only deployment assumption is not a capability guarantee.

Backup only: no normal implementation-branch push, PR, hosted review, merge or dispatch. The `-wip` branch remains because the user conditions its deletion on a normal branch push. Verification roots remain 7/20 and review roots 0/20; cold counts remain 0 comprehensive and 0 focused.
