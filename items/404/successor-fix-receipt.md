# Successor fix receipt

This change makes the B10 abrupt successor complete statistics while its native producer is still live, then dispose the producer, then complete evidence. The guard at the evidence-completion failure site now carries one fact per statistics diagnostic code, which raises the per-site fact cap from 16 to 35. It changes no protocol sequence, gate, timeout, interval, failure name or wire member.

## Execution card

- Deliverable and predicate map: requirements R1-R12, external contracts E1-E5 and tests T1-T13 of the [approved Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0b6a5cc56571090ff1402e291e901040c656f42f/items/404/plan-successor-fix.md). Only the files in its section 2.1 may change. Its test map is the predicate map of docs/agents/pull-requests.md; this receipt does not copy it.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754798641), [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6755620540) (`Blocking findings: none`) and the [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6755649081), which the control session posted under [standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 2; Hari has not reviewed it. Later decisions that alter this plan: none. The decisions the plan cites ([6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231), [6751199288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751199288), [6751449502](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751449502), [6752059755](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752059755), [6752078378](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752078378), [6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792), [6753364128](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753364128), [6754092685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754092685), [6754970055](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754970055)) and the order decisions [6752454732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752454732) and [6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218) were read in full. Candidate selection: three approved plans exist on the issue (this one, the capture contention race fix and PR3a); 6752454732 puts this one first.
- Mandatory gates: acceptance commands 1-13 of the plan, the 22 scratch mutations, cold review, final-head CI, hosted Codex code and security review, and the merge rule. No trial and no workflow dispatch.
- Verification commands: the plan's section 10 is the exact list. Mutations run in a separate `git worktree` of the committed head, so the repetition gate and the mutations never share a build output.
- Publication checkpoint: after local acceptance, mutation evidence, documentation checks and applicable cold-review coverage.
- Budgets and exhaustion: review findings, verification failures and documentation warnings are each capped at 20 root causes; exhaustion means `push -wip and hand off to the next session` (plan section 11(a)). Cold review permits at most two comprehensive and two focused rounds across all sessions, as docs/agents/pull-requests.md rule 8 sets.

## Authority and implementation choices

The plan file was read at commit `0b6a5cc56571090ff1402e291e901040c656f42f`; its SHA-256, `b41f90e4c05b53aaa25873e268e8554e8d4bfd2687d380e562592be0156ed0ac`, matches the plan comment and the approval. The issue carries `ready-for-agent`. The implementing tool is Claude Code under decision 6752430614 point 3, which replaces the original Codex assignment for this pull request. The issue's comment set was fetched again after implementation, before the cold review: no comment was added, edited or deleted after the 170 read at the start, and the issue body is unchanged. The refreshes before the push and before pull request creation are recorded in the pull request's self-review record.

Choices the plan leaves to the implementer (plan section 14), each checked by the cold review and Codex review:

- Completion behaviour: a two-member record, `B10AbruptProducer.SuccessorCompletion`, replaces the producer's single `completeEvidence` delegate. The producer completes statistics, then disposes, then completes evidence. The host builds the record in `B10AbruptHost.SuccessorCompletion`.
- Handler: `B10AbruptHost.StatisticsHandler` returns the closure that calls native-capture tracking and then the session. The native client's builder installs it.
- Facts seam (plan 7.1): `B10AbruptHost.CompletionFacts(completion, evidenceFaulted)` returns the 35 facts in E2 order from a private table that pairs each `B10AbruptFact` member with its code. T6 and T10 use it. The enum members are `MissingBrokerCount`, `DiagnosticCount` and 31 members named `Diagnostic` plus the code in Pascal case.
- Guard placement (plan check P2-3, applied): the R3 guard runs inside the evidence-completion member, after `evidence.CompleteAsync` returns, with the same shape as the two-fact guard on `main`. T9 and T12 therefore observe it when they call the completion members directly.
- Test clock wrapper: `HookClock` wraps any `TimeProvider`, passes everything through, and delivers one report when the first 10 ms timer is created.
- Delivery timing: `Chain.Deliver` sleeps 1 ms before it reads the clock, and the chain sleeps 2 ms between creating the session and fixing the client's creation time. The session accepts a report only when its native timestamp is strictly above a boundary taken a moment earlier, at whole-microsecond resolution; the sleeps keep that margin without changing what is delivered or when. The plan does not forbid them.
- T4 and `DisposalsAtEvidenceCompletion`: a thin recording wrapper around the production evidence-completion member records the dispose count at that moment. It adds no completion logic.
- T12 and T13 read private members by reflection, as the plan permits: the producer's stored completion record, its adapter, its native-producer factory, and the `statisticsHandler` field that Confluent.Kafka 2.16.0 fills with a closure over the handler the host installed (`Action<string>`, not the two-argument form). If an upgrade renames that field, T13 fails at the lookup.
- T6 uses `MemberData` with 34 index rows; its expected names are the pinned E2 list, not derived from production.
- T9 adds `MessageTimeoutMs` 1000 to the client configuration; the plan's T9 inputs do not list it. librdkafka 2.16.0 documents `message.timeout.ms` only as the limit on how long a produced message waits for delivery; T9 produces no message. Whether it has any other effect is not verified. Nothing else about the client differs from the plan.
- `B10AbruptProducerTests`: the fixture's evidence session, completion delegate, `Evidence` property and `DisposalsAtEvidenceCompletion` are removed with the moved test. The moved test keeps every assertion it had and adds the plan's: success and `request-lifecycle.jsonl` content for `NoError`; the `native_error_callback_observed` guard observation, the failing site and absent successor files for `Local_Fail`.

Plan check findings applied (docs/agents/factory.md "Item lifecycle" step 1):

- P2-1, the D6 addendum: session-side contention before capture closes (`KafkaStatisticsSession.cs` line 106 on `main`, against the gate holders at lines 185-204, 221-241 and 354-438) is fixed by neither race-fix pull request. It is accepted for the next trial by decision 6753263792 point 2, as corrected by decision 6754092685, and E2 fact `diagnostic_capture_contention` names it.
- P2-2, requirement coverage: R2's `capabilities.json` statement holds only when disposal also returns within the 10 s cleanup timeout. A disposal timeout throws `TimeoutException` before evidence completion and leaves no `capabilities.json`; this is deployment assumption D2.
- P2-3: applied as the guard placement above.
- P2-4, mutation section: the X1/H2 kill by T13 is scheduling-dependent. Under the reversed handler, the session worker can observe report 2 before `CaptureNative` stores its hash, which also yields guard reason 6. Observed kills: 20 of 20 runs of T13 on mutation X1, and one of one on H2. A survivor would count as a verification-failure root cause under the plan's cap. Acceptance command 13 prints the handler body so the cold review checks the order directly.
- Citation slips m1 and m2 need no action: the observer comment in `KafkaStatisticsSession.cs` is at lines 206-208 on `main`, and `test_produced_first_and_successor_transport` is defined in `test_b10_abrupt.py` (class `ProducedBytesCases`) and run by the check script through its `ExportCases` subclass.

Predicate map: the plan's test map (section 7.2) covers every changed predicate with the literal expected values and boundaries it states; this receipt adds none. The one new boundary, the cap, is pinned at 35 kept and the 36th dropped (T10, T11), with M12 killed by both. A T9 run with the accepted duplicate-key outcome of decision 6754970055 is a pass of the test, not of R1: no T9 run in this session produced it. The 50 repetition-gate runs passed, and a further 30 runs of T9 alone each passed with no accepted-outcome line in the test output.

## Verification

SDK `10.0.402` (`dotnet --version`). Base `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`. The commands below ran on source commit `99f45ebdea30b64084db54cd91c0b3c8b20b29a8`, which holds all production and test changes; the commit that adds this receipt and the state file changes no other file. `dockerd` was started before command 11.

| Plan command | Execution and measured result |
| --- | --- |
| 1 | `dotnet restore tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --locked-mode`: exit 0. |
| 2 | `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors. |
| 3 | `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`: total 499, succeeded 498, skipped 1, failed 0. The skip is the existing published-apphost case. |
| 4 | The 50-run loop of the plan: `50 Total: 44, Errors: 0, Failed: 0`. The loop ran on the final test source. |
| 5 | Fresh `mktemp -d`, then the exporter class: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`. |
| 6 | `check_b10_abrupt_producers.py` on the same directory: `Ran 12 tests`, `OK`, last line `b10_abrupt_produced_bytes_checked`. |
| 7 | `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`: `Ran 12 tests`, `OK`. |
| 8 | Architecture build: 0 warnings, 0 errors; `*B10Abrupt*` run: total 1, succeeded 1. |
| 9 | Section "Scratch mutations" below. |
| 10 | `npm run check:docs`: `docs/operations/b10-abrupt-contract.md: no issues found`. The 8 warnings are in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md`, `docs/operations/evidence/issue-211/receipt.md`); they take this one blanket line under docs/agents/pull-requests.md rule 8. Before the receipt existed, `items/404/state.md` also warned of a missing link target. |
| 11 | `./scripts/verify-dotnet.sh` with `dockerd` running: exit 0, `Verification passed.`, including the warning-as-error control. Final-head CI (`dotnet-verification.yml`) remains the authority. |
| 12 | `grep -n` of `src/Parchwell.LoadGen/Run/Abrupt/`: statistics completion is requested only in the successor path of `B10AbruptProducer.cs` (`CompleteStatistics`, after the cohort seal) and constructed only in `B10AbruptHost.SuccessorCompletion`, which only `CreateProducerAsync` calls. No first-instance code path references it. The host passes `StatisticsHandler(observations, statistics)` to the native builder. |
| 13 | The handler body is `(_, raw) => { observations.CaptureNative(raw); statistics.Capture(raw); }`: native-capture tracking, then the session, nothing else. |

Two build-time findings, both mine: the first build of the new test class failed on a raw-string literal with too many closing braces (CS9007) and on two analyzer rules (CA1861 constant array arguments). I replaced the literal with serialized objects and moved the arrays to static fields. That is one verification root cause of the cap of 20. A first T13 run failed because Confluent.Kafka stores the installed handler as `Action<string>`; the helper reads that field. Both fixes are inside the plan's left-open list.

## Scratch mutations

All 22 mutations of plan section 7.3 ran in a separate `git worktree` of source commit `99f45ebdea30b64084db54cd91c0b3c8b20b29a8`, one at a time. Each was applied with a script, built (0 warnings, 0 errors), run against `B10AbruptSuccessorCompletionTests` and `B10AbruptDiagnosticsTests`, and then reverted with `git checkout -- src`; the worktree was clean after the last one. Every mutation was killed by at least the tests the plan lists. One mutation failed a test the plan does not list for it (M2 also failed T1). No survivor, and no mutation was rerun to obtain a kill. X1's 20 extra runs are the plan check P2-4 measurement of scheduling dependence: T13 killed it in 20 of 20.


| Id | Mutation | Failing tests observed |
|---|---|---|
| M1 | Statistics completion moved after disposal | T1, T4 x2, T5 |
| M2 | Evidence completion moved before disposal | T1, T4 x2, T5 |
| M3 | Producer disposed first; statistics completion performed at the start of evidence completion (the `main` shape) | T1, T4 x2, T5, T9 |
| M4 | R3 guard removed: facts computed, no throw | T2, T5, T12 |
| M5 | Guard condition ignores the evidence fault | T5 |
| M6 | Fact members for `late_capture` and `capture_contention` swapped (emitted names change order) | T2, T5, T6 x34, T10 |
| M6s | Code strings of facts 6 and 7 swapped (names and order unchanged) | T6 x2 |
| M7 | Guard checks after evidence completion removed | T4 |
| M8 | Statistics-completion exception caught and replaced by an incomplete completion | T8 |
| M9 | `diagnostic_count` counts only listed codes | T6 x2 |
| M10 | Capture-only completion requested, without the broker inventory | T2, T12 |
| M11 | Handler skips native-capture tracking | T1, T2, T4 x2, T5, T8 |
| M12 | Per-site cap left at 16 | T2, T5, T10, T11 |
| M13 | Last flag (`broker_name_mismatch`) removed | T2, T5, T6 x34, T10 |
| M14 | Fact members for `invalid_json` and `duplicate_property` swapped | T2, T5, T6 x34, T10 |
| M14s | Code strings of facts 31 and 32 swapped | T6 x2 |
| M15 | `missing_broker_count` fixed at 0 | T2, T6 x33, T12 |
| X1 | Handler calls the session before native-capture tracking | T13 |
| X2 | R3 guard evaluated before evidence completion | T2, T5, T12 |
| H1 | Host passes an empty broker set to the completion behaviour | T12 |
| H2 | Host handler order reversed (session first) | T13 |
| H3 | Host passes only the first inventory broker | T12 |

## Cold review input

Round 1, comprehensive, of the entire authored pull request diff against `origin/main` (`f5cc496fa3cf845e68e400b5b65e557f121dd928`). The review diff covers the production and test changes, the contract line, `items/404/state.md` and this receipt. The reviewer is given the review-diff path and this receipt only. Specification sources: the approved Revision 7 plan, its plan check, the approval, and the decisions listed in the execution card. Results are recorded in the pull request's self-review record, not here (docs/agents/pull-requests.md rule 8).

Round 2, focused, requested after round 1 reported two P1 findings (state file omitted ten decisions and approvals; the receipt claimed refreshes that had not happened) and five P2 findings, all in prose or comments. Input revision: `HEAD` of the branch after the correction commit that follows `99f45ebdea30b64084db54cd91c0b3c8b20b29a8`. Comparison revision: the round 1 staged tree, whose full diff against `origin/main` is supplied as context. The reviewer receives the full pull request diff, the delta since round 1 and this receipt. Cumulative count after round 2: one comprehensive, one focused.

Round 3, focused (the second and last focused round), requested after round 2 reported one P1 (an unsupported claim about `MessageTimeoutMs`, now replaced by the documented meaning and a statement that other effects are not verified) and three prose P2 findings in the state file and receipt. It reviews only those edits plus unresolved findings. Cumulative count after round 3: one comprehensive, two focused.
