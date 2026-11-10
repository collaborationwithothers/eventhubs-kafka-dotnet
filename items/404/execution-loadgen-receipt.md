# B10 restart execution part 1: execution receipt

This change replaces the stop/checkpoint/pipe protocol with a producer child that journals and kills itself, a parent that observes and publishes, and a successor that validates the journal and resends unconfirmed attempts. It adapts chart and phase evidence to schema 3 while trial wiring, classification and scientific publication remain separate work. Consolidated review corrections pass all local acceptance and mutation gates; final authored documentation checks and the second comprehensive review remain pending.

## Execution card

- Deliverable: [execution part 1 Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fc1a4f36452b2d302f1d73987e1ba5eb967b3d65/items/404/plan-b10-execution-loadgen.md), sections 2 to 4, with [amendment 6776685705](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776685705).
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775850681), [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776159640), and the seven decisions adopted by that approval. The latest exact-revision [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776377996) states `Blocking findings: none`; its four `apply in implementation` findings apply.
- Plan commit: `fc1a4f36452b2d302f1d73987e1ba5eb967b3d65`. Exact-body SHA-256, recomputed by `sha256sum`: `728351add854338a840433ab47f4aad540cd4690ea2fbe0a17cbb01ca898272a`. Baseline: `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43`, the merged redesign.
- Branch: `codex/404-execution-loadgen`; backup: `codex/404-execution-loadgen-wip`. This continuation starts from `1b7ef642dcf28fe05d4c58c1a28409d2b84b1f12`, as [handoff 6777482103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6777482103) records. The continuation corrected the two identified fixture causes and isolated M10 before the first comprehensive review. The continuation consolidated that review's four code/test corrections, corrected the exposed fatal-capture test input, froze source and completed local acceptance and mutation gates. The next task is final authored documentation verification and the second comprehensive review. The current user brief authorizes that continuation without expanding scope.
- Mandatory gates: plan sections 8 and 9, including the real child/Kafka tests and all 21 manual mutations, the full .NET check, chart/phase checks, scope and ASCII checks, documentation scan, cold review, final-head CI and hosted Codex code/security reviews. Exact commands and their status are below.
- Earliest non-backup publication: the tested, cold-reviewed checkpoint under [pull-request rule 8](../../docs/agents/pull-requests.md). That checkpoint remains pending. The protected verifier rejection test means Hari merges the ready pull request; the implementing session does not merge.
- Cumulative cold-review budget: two comprehensive and two focused rounds, across all sessions. Consumed: one comprehensive and zero focused; these counts never reset on continuation. Another required review beyond that budget, or unsafe focused review, saves a backup and hands off to Hari. Plan section 11(a) separately caps verification-failure and review-finding root causes at 20 each, with exhaustion `push -wip and hand off to the next session`. The previous run exhausted its verification cap and completed that handoff. This fresh continuation has eleven new verification-failure root causes and five review-finding root causes (four defects and one separately retained approved-deletion flag); the two corrected carryovers are historical causes 19 and 20, not new causes. The historical ledger below remains intact.
- Models: Codex; the underlying model identifier is not exposed. Fresh implementation and verification agents use high effort. The first comprehensive cold review is complete; the second comprehensive review is planned.
- Predicate map: plan sections 8.1, 8.2, 8.2a and 3.14, mapped to concrete tests below. Production scientific-verifier predicates do not change. The protected test retains scientific-admission rejection assertions for the schema-3 diagnostic export.

## Cold-review input record

First round (R1): comprehensive, complete. Reviewed authored revision: `f2c1a6bc61157f2dec40cc79aa2f1aa665244472`; tested-code input: `1d6b5a381ee73c9d62ecde4d6f6decd965514b20`; comparison: `origin/main` at `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43`. Diff-body SHA-256: `912a1c489d42c6f9e902c246cb4233cb3da2be7267c5cc72cb0f9adbe883d00e`. The complete earlier authored report is retained [below](#earlier-complete-r1-report), followed here by a disposition for every finding. All reports started on R1 were complete before corrections began.

Second round (R2): consolidated comprehensive re-review planned; final code input is `7028773d8e0d12c3af6f37a6d917e314e742d46d`, including the fatal-capture fixture correction after `f1561886f79a7497fe29ff39142c0e723b55cc5f`; the full authored input commit and diff metadata will be supplied with the frozen review inputs after this receipt is committed. Comparison is current `origin/main` at `d06d64f5f3cd220abc012e1fa1c1553c2fdbe1e9`; the R1 comparison and approved-plan baseline remain the historical `2ed497d6` identified above. Its inputs will include the full authored diff, this receipt, the earlier complete R1 report and dispositions, and the approved plan and later decisions. The control session records the final authored revision with the review inputs; this receipt need not record the head commit that contains itself. The final R2 report belongs in the PR self-review record, not in its own reviewed receipt. Cumulative completed counts: one comprehensive, zero focused; R2 will consume the second comprehensive round. Tool-reported R1 review run time: not reported.

Authority refresh: the control session read at `2026-11-10T22:08:29Z` found issue 404 unchanged with 239 comments, newest 6777482103, listed parents unchanged (five comments on 347, four on 348), and no deleted comment. The approved plan, approval, newest exact-revision check, amendment and continuation authority remain unchanged. Current `main` advanced to `d06d64f5f3cd220abc012e1fa1c1553c2fdbe1e9` through governance cleanup PR 429, removing expired review-measurement prose/template fields without changing the review caps or publication checkpoint. The branch merges it without conflict at `9743f4e2ba8e06a284b796d198b59fbe66ab994c`. Script/runtime/code/test/chart/phase/skill/AGENTS bytes match tested `7028773d`; its measurements remain applicable after the merge, and the authored PR diff remains 48 allowed paths. R2 applies the current governing review text. No review count resets.

### R1 finding dispositions

| Finding / root cause | Disposition and present evidence |
|---|---|
| R1-1, P1: C# reader misses prepared-attempt reconciliation and final complete-control prerequisite | Fixed in the shared reader: reconcile every prepared attempt to acceptance or synchronous rejection, and require complete matched control before any cross-restart prepared record or kill intent. A terminal must ultimately have acceptance, preserving callback-before-acceptance. Generic accepted same-instance prefixes retain no added completion prerequisite. Isolated red/green evidence is below; final combined-source acceptance passes on `7028773d` as recorded below. |
| R1-2, P1: original pacing anchored before durable setup, with no post-delay deadline check | Fixed in production: each arm anchors to original zero's actual producer invocation, rounds a positive wait up to a whole millisecond and rechecks its deadline after every wake. Six actual-invocation regression cases cover both arms with durable-setup latency and early/quantized timers. All fail behaviorally on R1 and pass with the correction in the isolated 11-case run below; final corrected-source acceptance passes on `7028773d` below. |
| R1-3, P1: synchronous rejection never satisfies the control terminal check | Fixed in production: a durable synchronous rejection satisfies the attempt's terminal state without inventing a delivery callback; callback/rejection conflicts retain the first terminal and latch the existing duplicate guard. Five cases cover control originals zero through three and the selected resend, preserving ordinal-four cut rejection and cross-arm policy. All fail behaviorally on R1 and pass with the correction in the isolated 11-case run below; final corrected-source acceptance passes on `7028773d` below. |
| R1-4, P1: retained terminal theory loses independent delivered/error contradiction | Accepted. Restored independently supplied delivered input and the retained timed-out/PossiblyPersisted/delivered=true rejection case, preserving the prior eight cases and assertions. The focused terminal-tuple run passed all nine cases with zero skips after a zero-warning/error build; final full-source acceptance is also recorded below. |
| R1-5, P1: mandatory flag for tests deleted with retired protocol code | Kept under the explicit approval and pull-request rule 8 exception; this is not a `Not valid:` disposition. [Approved plan section 2.4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fc1a4f36452b2d302f1d73987e1ba5eb967b3d65/items/404/plan-b10-execution-loadgen.md#24-tests-deleted-with-the-code-they-test), [decision 6774981258 Q4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774981258), and [decision 6775792095](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775792095) explicitly authorize only methods whose subjects were removed and retain the per-method inventory below. Keep this finding separately in the eventual PR description and leave the merge to Hari. The unapproved retained-test loss in R1-4 is corrected separately. |

The five reported roots count against the plan's review-finding cap. The approved-deletion flag remains visible and separately identified; it does not request revival of the retired protocol. No PR exists, no normal push has occurred, and the remote backup remains `1b7ef642dcf28fe05d4c58c1a28409d2b84b1f12`.

## Implementation and plan-check dispositions

The child records `prepared`, `accepted`, terminal and native-error observations in `journal.jsonl`, with the journal lock ordering the control `cut` and its stored set. The parent starts one child, observes exit, checks identity and cgroup evidence, classifies termination, and publishes the journal, bound reports, manifest and termination receipt. The successor validates the authenticated inputs before constructing a producer, recomputes the kill set, and resends its members at the release boundary. Stop/resume signalling, pipe framing, checkpoints, cohort seals and kill-selection machinery are removed.

The schema amendment adds one adjacent `allOf` condition requiring a present, true diagnostic flag for either new abrupt mode. The chart tests add the four false/absent flag rejection cases. This resolves the scope conflict recorded before implementation resumed; the amendment leaves every other freeze and stop rule in force.

The exact-revision check's findings are disposed of as follows. The verification sections below distinguish preserved previous-run observations and pre-R1 acceptance from measured post-R1 checkpoints and completed final-source acceptance.

| Finding | Implementation choice and evidence |
|---|---|
| 1: unchanged worker bounds | Produce the schema-3 export in process, using production journal/publication/successor members and a fake clock, rather than waiting 120 s in the chart host bridge. The real-Kafka end-to-end test separately retains the real release delay. |
| 2: successor invocation checks | Phase collection pairs invocations in order with the accepted kill set and retains predecessor/new-attempt identity, key, sequence, payload hash and release-time checks. `test_produced_successor_invocation_payload_binding` changes the first payload hash to 64 zeroes and expects `b10_abrupt_successor_binding_invalid`. |
| 3: schema mutation isolation | The produced-bytes mutation changes `first/termination.json` to schema 2 so the envelope predicate is reached without an earlier descriptor-hash rejection masking it. |
| 4: real no-drain host | The real-process T-N3 child runs production host composition and lifetime, replacing only the producer factory with the recording seam. The test checks that neither Flush nor Dispose occurs after cross-arm admission, including process-exit handlers. |
| 5: Python method dispositions | Apply the retained-rule per-method rule to the phase test file as well as the C# files. The inventory below identifies every retained, adapted and deleted baseline method. |
| 6: obsolete cohort collector | Remove `collect_cohort_files` and its call; all first-pod journal/report members are downloaded and checked before successor release. |
| 7: a gap after kill intent | `gap` is not treated as a producing-thread-only kind because either thread can write it. A gap still invalidates capture and blocks successor. This choice must carry into part 2's plan. |
| 8: cross-process seams | Keep `ChildCommand.ForRunningApphost` with its existing full type name; use the existing startup-hook approach for recording seams, without adding abrupt configuration keys. |
| 9: documentation warnings | The initial full scan measured 10 warnings, all in unchanged files, and zero warnings in changed Markdown. They require one blanket explanation in the pull request under rule 8 and do not consume a disposition cap. The final authored pre-cold-review scan remains pending. |
| 10: authority/citations | Use the actual refreshed authority and exact pinned plan above. No approved-plan prose is edited in this implementation. The affected architecture and scientific-rejection gates pass in the full verification script. |

## Verification and continuation

Final corrected source freeze: `7028773d8e0d12c3af6f37a6d917e314e742d46d`. Its only change from correction checkpoint `f1561886f79a7497fe29ff39142c0e723b55cc5f` is the fatal-capture test input described below; both retained assertions and every other file are byte-identical. The four R1 corrections, test-only fixture correction and ten required mutation replays are complete at their named checkpoints; historical evidence remains scoped to its named revision, and the final acceptance below establishes the combined corrected source. Final authored documentation/ASCII checks and R2 also remain pending.

### Final corrected-source gates

Final tested code: `7028773d8e0d12c3af6f37a6d917e314e742d46d`; current merged branch: `9743f4e2ba8e06a284b796d198b59fbe66ab994c`, with byte-identical execution and verification inputs as recorded above. The fatal-capture test now obtains a production-generated complete control and kill intent, then uses the actual native-error queue, drain and persisted journal read. This preserves the original native-error-after-kill subject and both assertions, with no production change. File SHA-256: `27619f8ac4f92b2f083ed3fcfba9b0afa7097c47c839220148ca9f66ccb3c906`. Its isolated full fatal-capture class passes 12 cases with zero failures/errors/skips/not-run in 3.974 s after a zero-warning/error build in 21.22 s. The final full-source acceptance pass also covers this correction, resolving cause 11.

| Gate | Final-source status |
|---|---|
| Explicit acceptance-1 LoadGen build | At final `7028773d`, passed with zero warnings/errors in 15.56 s. |
| Exact `./scripts/verify-dotnet.sh` | At `7028773d`, actual tool exit 0; XML independently parsed by the control session: 10018 of 10018 passed, zero failures/errors/skips/not-run (Architecture 17, Enrichment 56, Kafka 491, LoadGen 1725, Scenarios 340, Verifier 7389). Full restore/build, test manifests, report validation and warning control all pass; the valid counterpart builds and intended CS1030 is rejected. Protected diagnostic rejection passes in 3.169 s and legacy roundtrip in 0.007 s. The full unfiltered LoadGen step also executes acceptance 2: 1725 passed, zero errors/failures/skips, MTP 8 min 00.201 s (XML 479.254 s). Published-apphost test runs unskipped and passes in 0.001 s; corrected fatal-after-kill test passes in 0.086 s. |
| All 21 mutation rows | Applicable: ten measured replays at `f1561886` plus eleven retained rows below. Only the unrelated fatal-capture fixture changes at final freeze; production, every mutation target/detector and host-bridge input are byte-identical to `f1561886`. |
| Acceptance-9 host bridge | Retains the fresh `f1561886` pass by byte-identical production/export/host input paths. |
| Static scope and final authored ASCII/documentation checks | Scope remains 48 paths because the fatal-capture file was already included; final receipt/state ASCII and documentation scan remain pending. |
| R2, final-head CI and hosted Codex code/security reviews | Pending; one comprehensive and zero focused cold rounds completed. |

The final script's LoadGen step records E1/E2 passing in 318.121 s with local observation `exit_code_local_observation=137 runtime=10.0.13 kernel=6.18.57 cut_accepted_to_kill_intent_microseconds=134978982.936`, followed by one consumed successor resend. T-N3 passes in 122.805 s, with its only control Flush preceding cross admission; the fixed 1000-iteration cut race passes in 112.476 s. These measurements apply to final tested `7028773d` and the execution-equivalent merge `9743f4e2`.

### Correction-checkpoint acceptance

The following measurements describe correction checkpoint `f1561886f79a7497fe29ff39142c0e723b55cc5f`. The fixed ten mutation replays are complete; the fatal-capture input correction is measured separately at the final freeze above and full final-source gates pass as recorded above. Targeted correction tests do not substitute for them.

| Command or gate | Corrected-source evidence |
|---|---|
| `dotnet build tests/Parchwell.LoadGen.Tests -c Release` | Passed with zero warnings/errors in 17.26 s. |
| `dotnet build src/Parchwell.Verifier -c Release` | Passed with zero warnings/errors in 9.07 s. |
| Full unfiltered LoadGen acceptance suite | At `f1561886`, 1725 total: 1723 passed, one failed, one designed published-apphost skip, zero runner errors. The failure is the hand-built native-error-after-kill fixture, whose cross-only journal omits the now-required complete matched control; no production change is proposed. The fixture owner preserves every original assertion and the native-error subject. The final `7028773d` script supplies the full passing run above. |
| `./scripts/verify-dotnet.sh` | Superseded by the final `7028773d` build and passing script above; its unfiltered LoadGen step also executes acceptance 2. |
| Static scope, ordinary-host freeze, retired-marker and ASCII checks | At `f1561886`: 48 approved changed paths, 45 surviving files and three deletions; no out-of-scope path or non-ASCII byte. Ordinary source diff is empty; Program.cs changes only line 44 and bytes outside baseline lines 41 to 48 are identical. Retired-marker matches: zero. Schema/phase permitted deltas match exactly. Architecture inventory remains 12 listed files out of 13 source files, with 60 matched sites and caller-information checks retained. Final receipt/state edits still require the final authored ASCII scan. |
| Chart and phase gates | Both trees are byte-identical from `f2c1a6bc` to `f1561886` and retain the earlier identical-source proof through `1e0719c6`: full chart gate passed, including 8 abrupt renders/54 invalid selections; phase suite passed all 284 tests. Ordinary-render byte-equality proof is retained. |
| Required host bridge | At `f1561886`, fresh exact acceptance-9 900 s wrapper passed with exit 0; all six host groups passed. Final line: `b10 abrupt host checks passed: 2 rendered environments, 7 rejected inputs, production-byte bridge`. The strict shared C# reader and independent phase reader accept the current production export bytes. |
| Final authored documentation scan | Pending. |
| Changed-source mutations | All ten fixed replays at `f1561886` reach the prescribed behavioral assertions after successful builds; every mutant is restored with clean status and byte equality. The complete row summary and eleven-row retention assessment are below. |
| R2, final-head CI and hosted Codex code/security reviews | Pending. One comprehensive and zero focused cold rounds completed; no PR or normal push. |

Despite the fixture failure, the same `f1561886` full acceptance run passes the real E1/E2 test in 324.978 s, recording local observation `exit_code_local_observation=137 runtime=10.0.13 kernel=6.18.57 cut_accepted_to_kill_intent_microseconds=134994745.191`, followed by one consumed successor resend. Real-host no-drain T-N3 passes in 124.225 s and the fixed 1000-iteration cut race passes in 114.202 s. These passing subjects do not make the failed full suite an acceptance pass.

The final gate sequence ran the explicit LoadGen build and then `./scripts/verify-dotnet.sh`. Lines 52 to 55 of the unchanged [verification script](../../scripts/verify-dotnet.sh) invoke the same full `dotnet test --project ... --configuration Release --no-build --minimum-expected-tests 1 --results-directory ... -- --report-xunit-xml --report-xunit-xml-filename results.xml` command as acceptance 2. Lines 46 to 50 publish and supply the apphost fixture. Its unfiltered full LoadGen run therefore serves acceptance 2 and the script's acceptance-3 gate together, with the published-apphost test required to run unskipped. No filter, required check or policy changes; the measured passing results above describe final test-corrected source `7028773d8e0d12c3af6f37a6d917e314e742d46d`.

### Post-R1 targeted correction evidence

These isolated checks establish their named corrections only. They are not the frozen corrected-source acceptance or R2, and consume no cold-review round.

| Correction and command | Measured result |
|---|---|
| R1-2/R1-3, `dotnet build tests/Parchwell.LoadGen.Tests -c Release -m:1 --disable-build-servers`, then compiled runner `-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptProducerPolicyTests -reporter verbose` | In `/tmp/404-producer-policy-regressions`, all 11 explicit new cases fail on old R1 production at their intended behavioral assertions: 11 failed, zero errors/skips, 0.572 s, after a zero-warning/error build. With only corrected producer source copied in, the build passes with zero warnings/errors in 22.35 s and all 11 cases pass with zero failures/errors/skips/not-run in 0.704 s. Tested producer SHA-256 `840a996d03f2d80e0a7a6ce8301e28ba5ed1430550746c7bb20ab6f239d6dba8`; new test-file SHA-256 `2d0a75bb6460a90d87b179b0ec2ac7b05f9a20df3556c252c347048fd39dbd01`. |
| R1-1, isolated R1 plus reader correction: LoadGen test build, then compiled runner selecting Journal, Supervisor, ParentProcess, Publication, WireProducer and SuccessorCompletion classes | On old R1 production, the journal class runs 59 cases: 24 expected new behavioral failures, zero errors/skips, 0.951 s; the two removed-ordinal-four-acceptance cases already reject under existing boundary logic and the three positive controls pass. With only the journal source/test correction applied, build has zero warnings/errors and six classes pass 223 cases with zero failures/errors/skips in 8.514 s. This run includes 29 added journal cases and does not cover the combined producer-policy and restored-tuple changes. Commands in `/tmp/404-journal-fix`: `dotnet build tests/Parchwell.LoadGen.Tests -c Release -m:1`; then test apphost `tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptJournalTests` for red, adding fully qualified `-class` selections for `B10AbruptSupervisorTests`, `B10AbruptParentProcessTests`, `B10AbruptPublicationTests`, `B10AbruptWireProducerTests` and `B10AbruptSuccessorCompletionTests` for green. Both use `DOTNET_ROOT=/home/agent/.parchwell/dotnet`; build uses that installation's `dotnet`. |
| R1-4, `dotnet build tests/Parchwell.LoadGen.Tests -c Release`, then compiled runner `-method Parchwell.LoadGen.Tests.Abrupt.B10AbruptSupervisorTests.Given_TerminalPersistenceTuple_When_SupervisorCaptures_Then_OnlyPermittedTuplePasses -reporter verbose -noColor -result-xml <scratch>` | In isolated R1 worktree `/tmp/404-tuple-restore-f2`, with only the restored theory applied, build has zero warnings/errors; all nine cases pass, with zero failures/errors/skips/not-run. XML parsing confirms execution of the independent contradictory tuple. |

The combined focused check in `/tmp/404-journal-fix` then copies the five current correction files (journal source/test, producer source/new policy test, restored supervisor theory), builds with `dotnet build tests/Parchwell.LoadGen.Tests -c Release -m:1`, and invokes the apphost with the prior six fully qualified class selections plus `Parchwell.LoadGen.Tests.Abrupt.B10AbruptProducerPolicyTests`. Build passes with zero warnings/errors in 13.69 s; all 235 cases pass with zero failures/errors/skips in 8.671 s. This verifies composition of the four corrections, while mandatory full gates and mutation evidence remain separate. The following byte hashes bind its input:

| Correction file | SHA-256 |
|---|---|
| `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptJournal.cs` | `dabb649d385e9c68a5fd2a34b464de2fea41f2e9262ec290194c3f4ef02d00ac` |
| `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptJournalTests.cs` | `3c6e598584aa8db445395742cbce9c7280e74cd988cec93195b612b0e6a3b459` |
| `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs` | `840a996d03f2d80e0a7a6ce8301e28ba5ed1430550746c7bb20ab6f239d6dba8` |
| `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerPolicyTests.cs` | `2d0a75bb6460a90d87b179b0ec2ac7b05f9a20df3556c252c347048fd39dbd01` |
| `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs` | `5a23789340f8e767a0aa2e298816019fa04bdfbbc740c4410647a84ac2a47da4` |

The producer-policy and terminal-tuple runs use `DOTNET_ROOT=/home/agent/.parchwell/dotnet` and that installation's `dotnet`. The producer-policy methods are `Given_FirstOriginalSetupTakes50Milliseconds_When_InvokingOriginals_Then_OffsetsStartAtItsNativeInvocation` (two arms), `Given_EarlyOrQuantizedTimerWake_When_InvokingOriginals_Then_EachNativeInvocationHonorsItsExactLowerBound` (two arms, each with early and quantized wake), and `Given_DurableSynchronousRejection_When_OtherControlAttemptsFinish_Then_ControlCompletesAndKillIsAdmitted` (control originals zero through three and resend ordinal four). They add new coverage without altering baseline methods or assertions. The retained terminal-tuple theory preserves all prior eight cases and assertions while adding the independently contradictory ninth case.

The reader's complete-control prerequisite checks the preceding same-instance originals zero through four, one nonempty checked cut, exactly the cut-member resends in order at or after release and before one successful zero-outqueue drain, one following post-drain report, and terminal-or-rejection completion of every control attempt. Existing report/guard failures continue to block successor permission. The diagnostic publication path is unchanged. This implements ADR-021 protocol rules 3, 5, 6, 7 and 14 and approved plan sections 3.5, 3.6, 3.11 and D5.

| New journal method | Added regression coverage |
|---|---|
| `Given_ProductionJournalWithPreparedOnlyCrossOriginal_When_Read_Then_CaptureBlocksSuccessor` | Remove a non-cut cross original's acceptance from production-generated complete evidence; block capture and successor. |
| `Given_CompleteJournalWithOneAcceptanceRemoved_When_Read_Then_AmbiguousAttemptBlocksSuccessor` | Eleven control original/resend and cross-original acceptance removals, with unrelated structural references kept valid. |
| `Given_IncompleteControlWithOtherwiseValidKill_When_Read_Then_CaptureBlocksSuccessor` | Fourteen control-exposure, resend, timing, terminal, drain and post-drain defects; each keeps the kill evidence otherwise valid. |
| `Given_CompleteControlWithSynchronousRejection_When_Read_Then_RejectionCountsAsTerminal` | Two valid controls, with a rejected original or selected resend. |
| `Given_AcceptedControlPrefixWithoutBoundary_When_Read_Then_NoCompletionRequirementIsInvented` | Valid accepted same-instance prefix without cross/kill admission. |

### Pre-R1 acceptance evidence

Pre-R1 source freeze: `1d6b5a381ee73c9d62ecde4d6f6decd965514b20`; all applicable local acceptance commands and 21 mutation rows were complete for that source; the first review then found the defects recorded above. The measurements in this subsection are preserved as pre-R1 evidence. Prior cgroup freeze: `9e17e201752def19fbcf35d20d6aa6f5d0f68eea`. The first continuation freeze was `1e0719c6b550e9e92c43654db955610539669b5a`; the cgroup freeze adds only the private capture fixture and its proof tests, and the final freeze changes only the cut-race clock fixture. Commands run in isolated detached worktrees, with `DOTNET_ROOT=/home/agent/.parchwell/dotnet` and that SDK plus `/tmp/parchwell-tools` on PATH. Docker tests use the local Unix socket. Program output remains outside the repository.

| Command or gate | Pre-R1 evidence at named source |
|---|---|
| `dotnet build tests/Parchwell.LoadGen.Tests -c Release` | At final `1d6b5a38`, passed with zero warnings/errors in 6.54 s. |
| `dotnet build src/Parchwell.Verifier -c Release` | At final `1d6b5a38`, passed with zero warnings/errors in 3.13 s. |
| `dotnet test --project tests/Parchwell.LoadGen.Tests -c Release --no-build --minimum-expected-tests 1 --results-directory <scratch> -- --report-xunit-xml --report-xunit-xml-filename results.xml` | At final `1d6b5a38`, passed: 1684 total, 1683 passed, zero failed and one designed published-apphost skip, in 7 min 20.567 s. The mandatory verification script supplies that fixture and executes it unskipped (below). Earlier failing attempts are recorded in the continuation cause ledger. |
| T-N3 and T-P2 | At final `1d6b5a38`, real production host no-drain passed in 123.065 s with no Flush or Dispose after cross admission. Self-kill local observation remains `exit_code_local_observation=137 runtime=10.0.13 kernel=6.18.57`. |
| T-E1/T-E2 at final `1d6b5a38` | Passed in 319.296 s: local observations `parent_exit=0 exit_code_local_observation=137 runtime=10.0.13 kernel=6.18.57 cut_accepted_to_kill_intent_microseconds=134987657.471`; `successor_exit=0 consumed_resends=1`. One kill-set member, real Kafka and real release delay; these are workspace observations. |
| Cgroup proof tests, compiled runner with `-class Parchwell.LoadGen.Tests.Abrupt.B10AbruptLocalCgroupCaptureTests -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptCgroupTests` | New and retained coverage passed: 41 cases, zero failures/skips, in 0.213 s. The final full suite separately executes this coverage. |
| `./scripts/verify-dotnet.sh` | At final `1d6b5a38`, passed with exit 0: XML-computed 9977 passed, zero failed/skipped (Architecture 17, Enrichment 56, Kafka 491, LoadGen 1684, Scenarios 340, Verifier 7389). Published-apphost test runs unskipped; protected diagnostic rejection passes in 3.671 s. Restore/build, test manifests, XML validation and warning negative control (valid counterpart passes; intended CS1030 rejected) all pass. |
| `git diff --name-only origin/main...HEAD` plus approved-path script | At `9e17e201`, 47 approved changed paths; zero out-of-scope paths. |
| `git diff origin/main...HEAD -- src/Parchwell.LoadGen ':!src/Parchwell.LoadGen/Run/Abrupt' ':!src/Parchwell.LoadGen/Program.cs'` | Empty at `9e17e201`. Program.cs differs only at line 44; bytes outside approved baseline lines 41 to 48, including reserved-environment refusal, are identical. |
| Plan section 9 retired-mechanism grep | At `9e17e201`, zero matches in production C# source. |
| `LC_ALL=C grep -n -P '[^\x00-\x7F]'` over surviving changed files | At `9e17e201`, zero matches across 44 files; three further changed paths are deletions. Final receipt/state edits still require refresh. |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-tools DOTNET_ROOT=/home/agent/.parchwell/dotnet python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh` | Passed at `1e0719c6`: `b10 abrupt host checks passed: 2 rendered environments, 7 rejected inputs, production-byte bridge`. Refresh passed at `9e17e201` with the same exact wrapper and final line; current host coverage is complete. |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-tools bash charts/parchwell/tests/check.sh` | Passed at `1e0719c6`, including 8 abrupt renders and 54 invalid selections. Chart/phase trees remain byte-identical at `9e17e201`; coverage retained. |
| `PATH=/tmp/parchwell-tools:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | Passed at `1e0719c6`: 284 tests in 23.097 s; unchanged-source coverage retained. |
| Ordinary chart baseline render comparison | At `1e0719c6`, byte equal to `2ed497d6`: loadgen 7552 bytes, loadgen2 7555 bytes; unchanged-source coverage retained. |
| `npm run check:docs` | At `1e0719c6`, exited 0 with 10 advisory warnings in six unchanged files and zero in changed Markdown. Final authored revision scan remains required; unchanged warnings take one blanket PR disposition. |
| Cold review; final-head CI; hosted Codex code/security reviews | R1 is complete on `f2c1a6bc`; R2 is planned. Cumulative completed cold rounds: one comprehensive, zero focused. Final-head CI and hosted reviews remain pending; no pull request exists. |

The mandatory script's separate E1/E2 run passes in 322.110 s and records `local_observation parent_exit=0 exit_code_local_observation=137 runtime=10.0.13 kernel=6.18.57 cut_accepted_to_kill_intent_microseconds=135012474.448`; `local_observation successor_exit=0 consumed_resends=1`. Its fixed 1000 race passes in 107.505 s. These observations are distinct from the direct acceptance-2 run above. Final verification worktree is clean; raw logs and XML remain scratch artifacts, not committed output.

The final static check also reconstructs phase.py with only the approved hook-line replacement, the schema with only approved bounds/enums and the amended allOf entry, and the architecture inventory from the unchanged regex. All match; ordinary `test_chart.py` remains byte-identical. The architecture inventory below remains current: 12 listed files, 60 matched sites, and unchanged caller-information/at-least-one assertions.

### Previous-run evidence

The previous run stopped before a frozen source revision was supplied, at its 20-root-cause verification cap. Its required backup and next-session handoff completed at `1b7ef642dcf28fe05d4c58c1a28409d2b84b1f12` and [handoff 6777482103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6777482103). The table below preserves that previous run's targeted evidence; it does not claim acceptance of the current continuation.

The observations below describe the uncommitted implementation working tree based on `9e1f235cb9773c809c0ba58cbc8d032b74e59d47`, including the coordinated v3 build before the handoff commit. They are targeted implementation evidence, not final-source acceptance. The fresh verifier received no freeze SHA and ran no acceptance command or mutation. No program output is committed.

| Command or gate | Observed result and remaining work |
|---|---|
| `dotnet build tests/Parchwell.LoadGen.Tests -c Release --no-restore -m:1` | Coordinated v3 build passed with zero warnings and errors in 8.82 s. The full acceptance build on a frozen revision remains required. |
| `dotnet build src/Parchwell.Verifier -c Release` | Initial run passed with zero warnings and errors; final-source acceptance remains required. |
| Journal/options classes through the compiled xUnit entry point | 125 cases passed. Exact targeted commands are recorded below. |
| Corrected fatal/publication classes through the compiled xUnit entry point | 25 cases passed. |
| Producer/supervisor classes through the compiled xUnit entry point | The v2 run had 107 cases with 3 failures from the existing synthetic-clock root cause; after its correction the selected four parameterized correction cases passed. This is not a subsequent full-class pass. The fixed 1000-iteration cut-race test passed in the v2 run. |
| Wire-producer class, with `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT=/tmp/404-wire-export-v3` | 49 cases passed in 2.534 s and wrote the schema-3 export. |
| Parent/proc/signal unit classes | 38 cases passed. |
| Real-process class | Six cases ran with one failure in the real-host no-drain test T-N3. The self-kill test recorded `exit_code_local_observation=137 runtime=10.0.13 kernel=6.18.57`; plan stop rule 5 is not triggered. This is a local observation, not a portable or deployed-image guarantee. |
| Real-Kafka end-to-end test | One test failed: expected `intended_self_kill`, observed `no_kill`. The successor verification did not complete. Root cause 20 below is identified and its fixture correction is unapplied. |
| `dotnet test --project tests/Parchwell.LoadGen.Tests -c Release --no-build` with Docker running | Not run as the full acceptance gate. `--project` is the current Microsoft.Testing.Platform CLI form for the plan's same test target; this is an execution syntax choice. |
| `./scripts/verify-dotnet.sh` | Not run on a frozen revision. |
| Scope, ordinary-host source freeze, removed-mechanism search and ASCII checks from plan section 9 commands 4 to 6 and 8 | Final-source checks not run; still mandatory. |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-tools DOTNET_ROOT=/home/agent/.parchwell/dotnet python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh` | The full required host bridge was not run; still mandatory. |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-tools bash charts/parchwell/tests/check.sh` | Passed, including `b10 abrupt chart checks passed: 8 renders, 54 invalid selections`. |
| `PATH=/tmp/parchwell-tools:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | Pinned-tool run passed: 284 tests. |
| `PATH=/tmp/parchwell-tools:$PATH PYTHONPATH=charts/parchwell/tests python /tmp/404-compare-ordinary-chart.py` | Working-tree render comparison against an archive of baseline `2ed497d6` passed byte for byte: `loadgen` 7552 bytes; `loadgen2` 7555 bytes. The diagnostic flag is false, binding is absent, and values omit `bounds` so each chart supplies its own defaults; unused abrupt bounds differ. |
| `PYTHONPATH=infra/scripts/phase/tests python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py /tmp/404-wire-export-v2` | 14 cases passed; the schema-3 reader accepted the produced bytes. This does not substitute for the required host bridge. |
| `npm run check:docs` | Initial run exited 0: 10 advisory warnings in unchanged files, zero in changed Markdown. A final scan before cold review remains required. |
| Cold review | Not started: zero of two comprehensive and zero of two focused rounds consumed. |
| Final-head CI and hosted Codex code/security reviews | Not started; no pull request exists. |

The architecture inventory was computed by the unchanged test regex over non-comment source lines and sorted by filename. It matched these files and site counts: `B10AbruptCgroup.cs` (2), `B10AbruptHost.cs` (4), `B10AbruptJournal.cs` (1), `B10AbruptOptions.cs` (1), `B10AbruptParent.cs` (2), `B10AbruptProc.cs` (1), `B10AbruptProcess.cs` (5), `B10AbruptProducer.cs` (22), `B10AbruptPublisher.cs` (6), `B10AbruptStatistics.cs` (12), `B10AbruptWire.cs` (2), `ChildCommand.cs` (2). `B10AbruptDiagnostics.cs` has zero matches and is excluded. Every surviving baseline-list file remains. The architecture test still requires caller information and at least one site per listed file. Its execution was pending at this historical handoff; the final pass is recorded above.

Targeted tests used the compiled entry point `dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll`, with `-class Parchwell.LoadGen.Tests.Abrupt.<class>` for each named class and `-reporter verbose` where recorded. The journal/options run selected `B10AbruptJournalTests` and `B10AbruptOptionsTests`; the corrected fatal/publication run selected `B10AbruptFatalCaptureTests` and `B10AbruptPublicationTests`; producer/supervisor selected `B10AbruptProducerTests` and `B10AbruptSupervisorTests`; wire selected `B10AbruptWireProducerTests`; the real-process and end-to-end runs selected `B10AbruptRealProcessTests` and `B10AbruptEndToEndTests` respectively. The correction run selected `Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly` and `Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains`. These targeted runs do not establish the unrun full-suite gates.

## Verification failures and budget

### Current continuation

New verification-failure root causes: eleven, counted from the shared continuation ledger below. Known carryovers: historical causes 19 and 20; both corrections are applied. T-N3, corrected E1/E2 and the full fixed-race test passed on pre-R1 source `1d6b5a38`; post-R1 acceptance now passes on final `7028773d`, including the unskipped published-apphost fixture. M10 input isolation is verified by its caught mutant, not an additional measured failure cause. New review-finding root causes: five, listed in the R1 disposition table above. Each category retains the plan section 11(a) cap of 20; exhaustion is `push -wip and hand off to the next session`. Repeat failures from an existing root cause are not counted again. Cold-review counts remain cumulative at one completed comprehensive and zero focused; R2 is planned.

| New root cause | Correction and status |
|---|---|
| 1. Detached docs worktree lacks local Node ESM dependencies | Private ignored symlink to pinned node_modules; exact npm scan then passed. No source change. |
| 2. Scratch render-hook checker matched generic mode conditions | Match the exact approved hook line; static check passed. No source change. |
| 3. Workspace cgroup namespace/mount mismatch blocks real E1 | Preserve production resolver rejection; add actual-counter test capture with FD-bound domain membership/identity proof. Targeted proof and real E1/E2 pass on the corrected freeze. |
| 4. M3 scratch anchor matched two ReleaseReached markers | Guard rejected edit before mutation; narrow to control-arm wait. Prescribed behavioral mutant then caught and restored. |
| 5. Initial M13 sub-millisecond Task.Delay mapping hangs | Discarded and restored; use precise injected-clock timer at release minus one tick. Prescribed early-resend behavior then caught. No shared code/test change. |
| 6. Scratch fallback checker guessed a lambda spelling | Read actual method-group fallback, correct scratch assertion; static check passed. No source defect. |
| 7. Full-suite fixed 1000-iteration race fixture times out in CleanupAsync | At `9e17e201`, TimeoutException precedes any behavioral assertion. Correct only the race fixture clock, preserving its exact 1 s timeout and all 1000 assertions; private fixture correction applied; final full LoadGen suite passes and fresh M18 is detected. Full .NET script also passes. |
| 8. Initial race-clock correction misses quantized release timer | Task.Delay drops sub-millisecond timeout precision, so an exact-only matcher misses the release wait. The invalid targeted run was stopped without pass/failure proof; the fixture now allows only the documented sub-millisecond truncation and jumps exactly to release, never early. Corrected fixed-race target and final full LoadGen suite pass. |
| 9. Isolated old-f2 producer-policy regression build loses MSBuild child nodes | The build reports MSB4166 for child nodes 2/3 exiting prematurely, with no source compiler diagnostic and no tests run. Its private MSBuild diagnostic directory disappeared before inspection; serial `-m:1 --disable-build-servers` retry passed with zero warnings/errors, enabling the 11 intended behavioral failures and corrected 11-case pass recorded above. The original build attempt supplies no behavioral result. |
| 10. Isolated journal regression helper violates CA1861 | The new helper passed a constant `new[]` control-ID argument; replace it with a collection expression in the owned test helper only. The first attempt stopped at analyzer build failure before any baseline red test. The collection-expression correction and subsequent isolated green build passed; the six-class 223-case pass is recorded above. |
| 11. Retained fatal-capture fixture omits the complete control before cross admission | Full acceptance at `f1561886` fails `Given_NativeErrorAfterKillRequest_When_SupervisorHandlesIt_Then_ItIsJournaled` at its existing empty-gap assertion because its manually constructed cross-only journal lacks the matched-control prerequisite. Correct only the fixture input, preserve every assertion and native-error-after-kill subject, then freeze and rerun the mandatory gates. At `7028773d`, the test uses production-generated complete control/kill evidence and the actual native-error capture queue, retaining both assertions unchanged. Full fatal class passes all 12 cases without errors/skips; final full gates pass on `7028773d` as recorded above. |

### Previous run (preserved)

The previous verifier's scratch ledger contains 20 distinct root causes, counted by script. The following authored summary preserves them without committing raw output. Earlier corrections have targeted evidence above, but no frozen-source acceptance; corrections 19 and 20 were unapplied at that handoff. Repeated failures from the same cause were not counted twice. That run exhausted its cap and stopped; its completed backup/handoff is linked above.

| Root cause | Correction or next-session action |
|---|---|
| 1. phase-test PATH missing pinned helm | Set PATH to pinned tool directory. |
| 2. chart test expected environment mode stale | Update in-scope chart test expected abrupt mode. |
| 3. render hook test seam incomplete | Correct test seam setup. |
| 4. removed production API still called | Update surviving in-scope production caller to replacement API. |
| 5. rewrite compilation and analyzer cleanup | Resolve rewrite compile/analyzer diagnostics before freeze. |
| 6. concurrent shared build output ABI race | Make tests parent sole shared build owner; freeze source before verification. |
| 7. sequence mutation input masked by malformed data | Correct test input so sequence-gap predicate is isolated. |
| 8. synthetic clock release drain deadlock | Correct synthetic clock/drain coordination; tests parent owns exact fix. |
| 9. real process helper runtime path unresolved | Set test helper child DOTNET_ROOT to pinned SDK/runtime installation. |
| 10. end-to-end scenario loader budgets missing | Supply existing scenario loader budget settings in test fixture. |
| 11. publication test selects duplicate equal report bytes | Select stored bytes by exact publication path key. |
| 12. real child telemetry connection missing | Supply required synthetic telemetry connection in child test environment. |
| 13. synthetic export child boot identity mismatches fake cgroup | Use actual child boot identity in synthetic cgroup observation. |
| 14. fatal capture fixture skips cross originals before ordinal four | Write valid ordered cross original sequence before kill intent. |
| 15. initial statistics report timestamp equals construction | Advance the test clock by 1 ms before observing the initial report. |
| 16. retained captured statistics client identity mismatch | Use fixture.Identity.ClientId for retained captured statistics identity. |
| 17. terminal after cut fixture reports same original twice | Track completion boolean and avoid duplicate terminal callback. |
| 18. oversize cleanup fixture exception text collides with run identity | Use unrelated fixture run identity to preserve original no-leak assertions. |
| 19. synthetic native statistics age uses absolute Stopwatch timestamp | Record fake producer creation timestamp and set native age to elapsed timestamp since producer creation. Unapplied at handoff. |
| 20. real Kafka test producer factory retains null Confluent configuration entry | Omit local.Set("sasl.oauthbearer.config", null!) or use typed SaslOauthbearerConfig=null to remove the entry; correction not applied. Unapplied at handoff. |

## Manual mutations

The table and changed-source assessments in this section preserve proof through pre-R1 source `1d6b5a38`. Post-R1 production and detector changes require the impact assessment and applicable replays below before these rows establish coverage of the corrected source.

Each mutation was applied separately in an isolated worktree, compiled successfully, failed the named behavioral assertion and was restored before the next mutation. M1 to M18 ran at `1e0719c6`; every prescribed target had passed in that freeze's full-suite attempt, although the independent E1 cgroup gate had failed. No full-baseline pass is claimed for that attempt. M19 and M21 retain the previous run's unchanged-source proof; M20 also has a fresh exact host-bridge failure at `1e0719c6`.

Commands: each M1 to M18 used `dotnet build tests/Parchwell.LoadGen.Tests -c Release -m:1` (or `--no-restore` after restore), with zero warnings/errors, then `dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -method Parchwell.LoadGen.Tests.Abrupt.<class>.<method> -reporter verbose`. The concrete detector names are mapped in the test tables below. Every mutation test exited 1 through its behavioral assertion, with zero runner errors; compile failures do not count. Restored tracked worktrees were empty, and touched files equalled frozen blobs byte for byte.

| Mutation | Required test / observed behavioral failure |
|---|---|
| M1: admit exit 0 as intended | T-P5 exit-0 row: `Assert.Equal`, expected `unexpected_exit`, actual `intended_self_kill`. |
| M2: omit last-producing-record check | T-P5 producing-after-intent row: expected `unexpected_exit`, actual `intended_self_kill`. |
| M3: compute control set at release | T-C2 terminal-after-cut row: `Assert.Single`, required prepared resend absent. |
| M4: omit control resend completed after cut | Same T-C2 row and missing-resend assertion. |
| M5: admit torn final record | T-J2 torn row: `Assert.Contains`, `malformed_frame` absent. |
| M6: admit sequence gap | T-J2 skipped row: `Assert.Contains`, `sequence_gap` absent. |
| M7: queue full at 256 | T-J4 count-256 row: expected valid true, actual false. |
| M8: successor with empty kill set | T-P7: `Assert.False`, actual true. |
| M9: termination before manifest | T-P8 upload-order test: expected `create:abrupt-manifest.json`, actual `create:termination.json` at index 10. |
| M10: omit report-binding comparison | T-P8 report_byte and report_length rows: `Assert.Throws`, expected InvalidDataException absent. Manifest descriptor checks and membership/existence checks remain enabled. |
| M11: omit termination hash check | T-S1 hash row: expected InvalidDataException absent. |
| M12: omit recomputed-set comparison | T-S3 kill-set row: expected `selection_stale`, actual later `receipt_changed`. |
| M13: resend one tick before release | T-S5 precise clock timer: `Assert.Empty` finds two resend Message entries at release minus one tick. The discarded Task.Delay mapping hung without an assertion and is excluded from proof. |
| M14: admit ordinary parent Job name | T-O4 parent/loadgen row: expected InvalidDataException absent. |
| M15: admit Role supervisor | T-O1 supervisor row: expected InvalidDataException absent. |
| M16: Flush after cross admission | T-N3 real host/lifetime: `Assert.DoesNotContain` finds Flush after cross_produce; 122.805 s. |
| M17: exit 3 after returned kill | T-K2 real child: expected exit 4, actual 3. |
| M18: compute cut set outside journal lock | Original proof and one fresh replay at final `1d6b5a38`: build zero warnings/errors, one detector invocation fails `Assert.Empty` with `malformed_frame` at line 268 (test 0.199 s). It stops at the first assertion; no claim of 1000 mutant iterations. Journal/detector restored byte equal, tracked worktree clean. |
| M19: phase accepts schema 2 | T-H3 `ProducedBytesCases.test_produced_mutations_are_not_accepted`: `AssertionError: ValueError not raised` on termination.json. |
| M20: chart emits supervisor flag | Historical T-H1 exact args assertion failed. Fresh exact acceptance-9 wrapper also exited 1 at host test line 55, its exact parent args assertion. Restored jobs.yaml SHA-256 equals frozen `80fe70a36dcc77d3e9a174d0ef0dbbf23f4d51ff3158d38f9263e7a79e6b0be8`. |
| M21: render hook includes ordinary loadgen | `ByteIndependentCases.test_render_hook_selects_only_abrupt_modes`: `Expected 'values' to not have been called. Called 1 times.` |

Changed-source assessment at `9e17e201`: the classifier, producer, publisher, options, journal, all M1 to M18 detector methods and all chart/phase targets/detectors are byte-unchanged. M16/M17 use non-Kafka modes and do not enter the new real-Kafka/realtime/parent cgroup hook. Coverage at that freeze remains valid. The race-fixture correction changes the M18 detector; the fresh final-freeze replay above restores its coverage. At `1d6b5a38`, all other production/chart/phase predicates and detector assertions remain unchanged, and default Fixture/StartReports callers retain the equivalent advancing behavior. All 21 rows have applicable coverage. The mandatory host-bridge refresh passed at `9e17e201`.

### Corrected-source mutation impact

The correction freeze is `f1561886f79a7497fe29ff39142c0e723b55cc5f`. The fixed replay set is ten rows: M3, M4, M5, M6, M10, M11, M12, M16, M17 and M18. They traverse the changed producer/reader paths; each needs a new failing behavioral detector on the frozen source and restoration proof. All ten rows have fresh behavioral detections and restoration proof at `f1561886`. No compile/setup/crash/timeout result counts as detection; every prescribed mutant builds with zero warnings/errors and every compiled detector exits 1 through the intended assertion, with zero runner errors/skips/not-run.

| Replay at `f1561886` | Behavioral detector result |
|---|---|
| M3 | T-C2 terminal-after-cut row: required prepared resend is absent, `Assert.Single` at producer test line 169; the other row passes. |
| M4 | Same T-C2 row and direct missing-resend assertion; the other row passes. |
| M5 | T-J2 torn row: `Assert.Contains` cannot find `malformed_frame`; other three rows pass. |
| M6 | T-J2 skipped row: `Assert.Contains` cannot find `sequence_gap`; other three rows pass. |
| M10 | T-P8 `report_byte` and `report_length`: `Assert.Throws` finds no InvalidDataException; membership/existence and descriptor checks remain enabled and other two rows pass. |
| M11 | T-S1 hash row: `Assert.Throws` finds no InvalidDataException; other three binding rows pass. |
| M12 | T-S3 kill-set row: expected `selection_stale`, actual later `receipt_changed`; other three binding rows pass. |
| M16 | Real T-N3: `Assert.DoesNotContain` finds Flush after cross_produce; test 123.371 s, runner 123.570 s, observed exit 3 and cross-admitted cut-rejected no-kill. |
| M17 | Real T-K2: `Assert.Equal`, expected exit 4, actual 3; test 2.907 s, runner 3.069 s. |
| M18 | One unchanged fixed-race detector invocation stops at `Assert.Empty` with `malformed_frame`; test 0.269 s, runner 0.433 s. The mutant does not claim 1000 completed iterations. |

All ten replays use isolated worktrees at the named freeze, `DOTNET_ROOT=/home/agent/.parchwell/dotnet` and pinned tools on PATH. Builds use `dotnet build tests/Parchwell.LoadGen.Tests -c Release -m:1` (later B-series rows add `--no-restore`); each then invokes the compiled LoadGen test DLL with `-method Parchwell.LoadGen.Tests.Abrupt.<class>.<method> -reporter verbose`, using the exact detectors already mapped above. Source and all detector/harness bytes equal frozen blobs after every restoration, and final tracked worktrees are clean. The earlier nonunique scratch M3 anchor is narrowed to the already-established exact control wait context, preserving its prescribed behavior and remaining cause 4; no new cause is counted.

Final freeze `7028773d` changes only the unrelated fatal-capture fixture; all production, mutation target and detector bytes remain equal to `f1561886`, so both the ten replays and the eleven retained proofs apply to that final source.

The other eleven rows retain their named-source proof with this source/path assessment:

| Rows | Retention basis at corrected freeze |
|---|---|
| M1, M2, M8 | Production target and detector source are unchanged; parent detectors construct snapshots directly and do not enter the changed reader. |
| M7 | Queue-boundary detector bypasses the shared reader; queue target and detector are unchanged. |
| M9 | Publication-order target and detector are unchanged. Its complete capture input remains valid in the combined 235-case pass; ordering is independent of the new reader reconciliation. |
| M13 | Successor release target and detector are unchanged; producer edits affect original-arm pacing and synchronous-rejection bookkeeping. |
| M14, M15 | Options targets and detectors are unchanged. |
| M19, M20, M21 | Chart/phase targets and detectors are unchanged. The mandatory corrected-source host bridge is still rerun separately. |

## Test map and method dispositions

The tables below are authored inventories against baseline `2ed497d6`, collected by script. They implement plan section 2.4 and [the per-method decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775792095), rather than deleting retained-rule coverage. [Decision 6774981258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774981258) accepts the removed-mechanism C# deletions; [decision 6775474492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775474492) and plan-check finding 5 cover the phase adaptation. Every deleted method names the mechanism that the redesign removes.

### Options, journal, publication and retained coverage

| Plan row | Actual test |
|---|---|
| T-O1 | `B10AbruptOptionsTests.Given_ValidRoleInputs_When_Validated_Then_OnlyThatRoleIsAdmitted`; `Given_RemovedOrCaseChangedRole_When_Validated_Then_NamesRole` |
| T-O2 | `B10AbruptOptionsTests.Given_ChildFieldMissingOrUsedByPodRole_When_Validated_Then_FailsClosed` (eight removed settings x three roles) |
| T-O3 | `B10AbruptOptionsTests.Given_ChangedFixedPolicy_When_Validated_Then_EachBoundaryFailsClosed` (all nine fixed policies below/at/above) |
| T-O4 | `B10AbruptOptionsTests.Given_JobNameForRole_When_Validated_Then_OnlyItsOwnJobIsAdmitted` (all three roles x four Job names) |
| T-J1 | `B10AbruptJournalTests.Given_TwoWritingThreads_When_RecordsInterleave_Then_All1020LinesAreCompleteAndSequential` |
| T-J2 | `B10AbruptJournalTests.Given_DamagedJournal_When_Read_Then_CaptureGapIsLatched` (empty/torn/malformed/skipped); `B10AbruptParentProcessTests` capture gap classifier row separately blocks successor |
| T-J3 | `B10AbruptJournalTests.Given_RecordByteBoundary_When_Appended_Then_OnlyAboveBoundLatchesGap`; `Given_JournalByteBoundary_When_Admit_Then_ChargesBeforeWrite`; `Given_JournalBytesAtLimit_When_AnotherRecordArrives_Then_OnlyAboveLimitLatchesGap`; `Given_JournalEntryBoundary_When_Append_Then_4097thEntryCannotBecomeClean` |
| T-J4 | `B10AbruptJournalTests.Given_TerminalQueueBoundary_When_Capture_Then_OverflowIsSticky` (255/256/257 with blocked writer and unchanged file length); `B10AbruptFatalCaptureTests.Given_FullWriteQueue_When_NativeErrorArrives_Then_QueueFullIsLatchedAndWrittenAsCaptureGap` |
| T-P8 | `B10AbruptPublicationTests.Given_CohortFileChangedAfterSeal_When_Publishing_Then_NothingBeyondTheRegistrationIsPublished` (journal/report byte mismatch, missing bound report, report length); `Given_CohortFileUploads_When_Publishing_Then_EachAttemptIsOnStderrBeforeTheNextUpload` (exact create/read order ending termination) |
| T-S1 | `B10AbruptPublicationTests.Given_ChangedSuccessorBinding_When_Loaded_Then_FailsBeforeRegistration` `hash` row changes exactly one hex digit |
| T-S2 | same test `not-permitted` row; no storage Create call |
| T-S3 | same test `kill-set` row, receipt hash rebound so only recomputation can reject |
| T-S4 | same test `job` row |
| R19 | `B10AbruptFailureSiteTests.Given_AbruptFailureExceptions_When_SitesAreScanned_Then_EverySiteCarriesCompilerCallerInformation`; `B10AbruptRejectionTests.Given_ProductionDiagnosticBytes_When_CopiedOrRelabelled_Then_AllScientificAdmissionRejects` |
| T-S5 | `B10AbruptSuccessorCompletionTests.Given_TwoKillSetMembers_When_SuccessorWaitsAtReleaseBoundary_Then_ResendsEachOnceWithBoundPayload` (1ms before, 1tick before, at release; two bound resends) |

| Baseline file | Method | Disposition / new home |
|---|---|---|
| B10AbruptJournalTests.cs | Given_PartialFrameAtStoppedBoundary_When_RemainingBytesArrive_Then_RetainsAndCapturesExactlyOnce | deleted: removed IPC pipe framing/partial transport reader |
| B10AbruptJournalTests.cs | Given_PartialAvailableFrame_When_PipeCloses_Then_CaptureRemainsIncomplete | deleted: removed IPC pipe framing/partial transport reader |
| B10AbruptJournalTests.cs | Given_InvalidAvailableLength_When_Draining_Then_RejectsBeforeReadingBody | deleted: removed IPC pipe framing/partial transport reader |
| B10AbruptJournalTests.cs | Given_BoundedBody_When_Read_Then_AcceptsExactlyPrefixLength | adapted to B10AbruptJournalTests.Given_RecordByteBoundary_When_Appended_Then_OnlyAboveBoundLatchesGap (wire JSON byte limit retained) |
| B10AbruptJournalTests.cs | Given_InvalidLength_When_Read_Then_RejectsBeforeBodyAllocation | adapted to B10AbruptJournalTests.Given_RecordByteBoundary_When_Appended_Then_OnlyAboveBoundLatchesGap and Given_NonClosedJson_When_Read_Then_Rejects (wire byte and malformed bounds retained) |
| B10AbruptJournalTests.cs | Given_PartialTransport_When_Eof_Then_Fails | adapted to B10AbruptJournalTests.Given_DamagedJournal_When_Read_Then_CaptureGapIsLatched (torn JSONL record) |
| B10AbruptJournalTests.cs | Given_EmptyTransport_When_Read_Then_ObservesEof | adapted to B10AbruptJournalTests.Given_DamagedJournal_When_Read_Then_CaptureGapIsLatched (empty journal invalid) |
| B10AbruptJournalTests.cs | Given_NonClosedJson_When_Read_Then_Rejects | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_DuplicateOrSkippedSequence_When_Read_Then_LatchesGap | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_JournalByteBoundary_When_Admit_Then_ChargesBeforeWrite | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_TerminalQueueBoundary_When_Capture_Then_OverflowIsSticky | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_JournalEntryBoundary_When_Append_Then_4097thEntryCannotBecomeClean | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_DurableAppend_When_Returns_Then_ReadbackContainsExactClosedFrame | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_FlushHeld_When_Append_Then_NoDurableAcknowledgementBeforeFlush | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptJournalTests.cs | Given_WriterFailure_When_Append_Then_GapCannotBeClearedByLaterRecord | retained/adapted in B10AbruptJournalTests.cs |
| B10AbruptOptionsTests.cs | Given_ValidRoleInputs_When_Validated_Then_OnlyThatRoleIsAdmitted | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_MissingRequiredOrPresentRejectedBinding_When_Validated_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_IncompatibleDiagnosticInput_When_Validated_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_MissingDiagnosticAuthorization_When_Validated_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_NoDiagnosticEnvironment_When_Read_Then_DisabledAndCannotEnterDiagnosticHost | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_UnknownCaseCollidingOrMixedPrefix_When_Read_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_DuplicateSetting_When_Read_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ChildFieldMissingOrUsedByPodRole_When_Validated_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_MalformedChildHandshake_When_Validated_Then_FailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ChangedFixedPolicy_When_Validated_Then_EachBoundaryFailsClosed | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ReservedPrefix_When_OrdinaryEntryRuns_Then_RejectsBeforeHostOrProducerConstruction | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_NoDiagnosticFlag_When_OrdinaryEntryRuns_Then_ReachesOnlyOrdinaryHostConstruction | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ValidConfigurationWithUnmountedBindings_When_ValidatorEntryRuns_Then_ValidatesWithoutOpeningThem | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_InvalidConfiguration_When_ValidatorReportsFailure_Then_NamesPredicateWithoutInputValues | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_UnexpectedExceptionWithUntrustedDetail_When_Reported_Then_EmitsOnlySafeFallback | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ThrownSecretBearingException_When_Reported_Then_EmitsClosedLabelAndTypeOnly | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ExplicitConfigurationFailureWithUntrustedDetail_When_Reported_Then_EmitsUnclassifiedAndRealType | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_Cancellation_When_Reported_Then_EmitsDeadlineAndTypeOnly | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ExactNoUnconfirmedAttemptFailure_When_Reported_Then_PreservesOnlyItsAuthorizedSuffix | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptOptionsTests.cs | Given_ControlCheckpointSelection_When_ReleaseRead_Then_UsesFirstSelectedMemberOrRejectsEmpty | retained/adapted in B10AbruptOptionsTests.cs |
| B10AbruptPublicationTests.cs | Given_ProducedFirstInstance_When_Published_Then_CohortFilesAreBoundAndFatalCoverageIsJournal | retained/adapted in B10AbruptPublicationTests.cs |
| B10AbruptPublicationTests.cs | Given_CohortFileChangedAfterSeal_When_Publishing_Then_NothingBeyondTheRegistrationIsPublished | retained/adapted in B10AbruptPublicationTests.cs |
| B10AbruptPublicationTests.cs | Given_PredecessorBinding_When_CarryingACohortFile_Then_SuccessorRejectsReceiptChanged | retained/adapted in B10AbruptPublicationTests.cs |
| B10AbruptPublicationTests.cs | Given_NativeErrorsBeforeAndAfterTheKill_When_EventStreamIsDrained_Then_TerminationCountsThemFromTheJournal | retained/adapted in B10AbruptPublicationTests.cs |
| B10AbruptPublicationTests.cs | Given_CohortFileUploads_When_Publishing_Then_EachAttemptIsOnStderrBeforeTheNextUpload | retained/adapted in B10AbruptPublicationTests.cs |
| B10AbruptPublicationTests.cs | Given_SecondCohortUploadFails_When_Publishing_Then_ItsLineIsOnStderrBeforeTheAbortPathTouchesTheChild | retained/adapted in B10AbruptPublicationTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_RealFatalNativeError_When_HostHandlerRuns_Then_JournalHoldsExactlyOneNativeErrorFrame | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_NativeErrorsOutOfSequence_When_SupervisorHandlesThem_Then_SequenceGapIsLatched | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_FirstNativeErrorNotNumberedOne_When_SupervisorHandlesIt_Then_SequenceGapIsLatched | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_NativeErrorAfterKillRequest_When_SupervisorHandlesIt_Then_ItIsJournaled | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_ControlSealCountingAnUnjournaledNativeError_When_Loaded_Then_ControlIncomplete | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_ControlSealWithPlaceholderFatalStatus_When_Loaded_Then_ControlIncomplete | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_TimeoutSealWithBadFatalCoverage_When_Loaded_Then_ControlIncompleteNamesTheFatalFact | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_ConcurrentCallbacksAndCountReads_When_Enqueuing_Then_EveryCountedFramePrecedesTheNextMarker | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_FullWriteQueue_When_NativeErrorArrives_Then_QueueFullIsLatchedAndWrittenAsCaptureGap | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_HostBuiltProducer_When_NativeFatalErrorIsRaised_Then_ChannelCarriesEveryCallbackAndCountSourceReadsIt | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptFatalCaptureTests.cs | Given_NativeErrorCountThatChangesDuringCohortRuns_When_SealsAreBuilt_Then_EachSealCarriesItsOwnCount | retained/adapted in B10AbruptFatalCaptureTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_LiveProducerAfterSeal_When_SuccessorCompletes_Then_StatisticsCompleteBeforeDisposalAndEvidenceAfter | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_NoPostBoundaryReport_When_SuccessorCompletes_Then_GuardFailsWithTimeoutFacts | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_EvidenceFaultDuringDisposal_When_SuccessorCompletes_Then_GuardFailsWithEvidenceFaulted | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_StatisticsCompletion_When_GuardFactsAreBuilt_Then_EachFlagMapsOneCode | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_StatisticsCompletionThrows_When_SuccessorCompletes_Then_ExceptionPropagatesAndProducerDisposedOnce | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_RealNativeProducerWithoutBroker_When_CompletionMembersRun_Then_StatisticsCompleteWhileLiveAndEvidenceAfterDisposal | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_AllStatisticsCodes_When_GuardFactsAreAttached_Then_CapKeepsAllThirtyFive | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_HostBuiltSuccessor_When_StatisticsCompletionRunsWithoutReports_Then_MissingBrokersEqualHostInventory | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptSuccessorCompletionTests.cs | Given_HostBuiltSuccessorClient_When_SessionCaptureFails_Then_NativeCaptureWasTrackedFirst | retained/adapted in B10AbruptSuccessorCompletionTests.cs |
| B10AbruptCgroupTests.cs | Given_LocalCounter_When_Parse_Then_PreservesIntegralCount | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_InvalidCounter_When_Parse_Then_FailsClosed | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_ResetOrIncrement_When_Compare_Then_Rejects | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_UnchangedCounterAndIdentity_When_Compare_Then_PassesOnlyExclusionCheck | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_ChangedIdentity_When_Compare_Then_Rejects | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_CgroupMountRoot_When_Resolve_Then_UsesLocalDirectory | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_ReadOnlyDirectoryHandle_When_LocalFileChangesOrDirectoryIsReplaced_Then_BindsInodeAndLocalCounter | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_OnlyHierarchicalCounter_When_CaptureLocal_Then_DoesNotSubstitute | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptCgroupTests.cs | Given_UnresolvedMembership_When_Resolve_Then_Rejects | retained/adapted in B10AbruptCgroupTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_GateHeldByAnotherThread_When_NativeReportCaptured_Then_CaptureReturnsAndReportStillMatches | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_ReportsCapturedWhileGateHeld_When_ObservedInCaptureOrder_Then_AllMatchInOrder | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_ReportsCapturedWhileGateHeld_When_ObservedOutOfCaptureOrder_Then_GuardReasonSix | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_PendingCapturesWhileGateHeld_When_BoundReached_Then_OnlyAboveBoundFails | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_TrackingNotActivated_When_NativeReportCaptured_Then_GuardReasonFour | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_MoreReportsThanBoundCapturedAndObservedInTurn_When_Checked_Then_NoGuardFailure | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_TrackedAdapterWithNoPendingReport_When_Observed_Then_GuardReasonSix | retained/adapted in B10AbruptStatisticsContentionTests.cs |
| B10AbruptStatisticsContentionTests.cs | Given_RealNativeReportsAndContinuousPostDrainScan_When_ReportsArrive_Then_NoGuardFailureAndEveryReportMatched | retained/adapted in B10AbruptStatisticsContentionTests.cs |

### Process methods

| Plan row | Concrete test |
|---|---|
| T-P1, T-P2, T-K1 | `B10AbruptRealProcessTests.Given_RealChild_When_SelfKillFollowsDurableIntent_Then_ParentObserves137AndCompleteJournal` |
| T-P3 | `B10AbruptRealProcessTests.Given_RealSleepingChild_When_ParentWaitBoundExpires_Then_UnknownChildRemainsAliveUntilTestCleanup` |
| T-P4 | `B10AbruptParentProcessTests.Given_MatchingSelfKill_When_ParentClassifies_Then_SuccessorIsPermitted` |
| T-P5 | `B10AbruptParentProcessTests.Given_UnintendedExitOrProducingAfterIntent_When_ParentClassifies_Then_ExitIsUnexpected` (exit 134, exit 0, producing record after intent) |
| T-P6 | `B10AbruptParentProcessTests.Given_DurableNoKill_When_ParentClassifies_Then_ExitCodeDoesNotGateNoKill` (exit 3 and 1) |
| T-P7 | `B10AbruptParentProcessTests.Given_IntendedKillWithNoUnconfirmedAttempt_When_ParentClassifies_Then_KillTooLateBlocksSuccessor` |
| T-K2 | `B10AbruptRealProcessTests.Given_ReturnedKillInRealChild_When_ParentClassifies_Then_ActualSignalFailureIsRetained` |
| T-N1 | `B10AbruptRealProcessTests.Given_RealChildBeforeCrossAdmission_When_ControlCannotContinue_Then_NoKillExitsThreeAndDisposes` (`empty` row) |
| T-N2 | `B10AbruptRealProcessTests.Given_RealChildBeforeCrossAdmission_When_ControlCannotContinue_Then_NoKillExitsThreeAndDisposes` (`incomplete` row) |
| T-N3 | `B10AbruptRealProcessTests.Given_ProductionHostAfterCrossAdmission_When_NoKillExits_Then_ProcessExitHandlersDoNotFlushOrDisposeProducer` |
| T-E1, T-E2 | `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes` (one sequential parent/child then successor run) |

All classes are in `Parchwell.LoadGen.Tests.Abrupt`. T-P1 records the parent's native-signal boundary calls with `B10AbruptProcess.SignalOverride` and requires none, records the child PID through the startup hook and requires exactly one, and requires `/proc/<pid>` to be absent after the parent returns. The child process uses its own static signal seam state, so its self-SIGKILL remains real. T-N3 changes only the producer factory; its host service composition, lifetime and 120 s release remain production behavior, and the producer recording file checks Flush and Dispose through process exit. T-E1 and T-E2 use real Confluent.Kafka against the unchanged LocalKafkaFixture, the real 120 s release and file storage in the successor process. The E2 payload check requires byte equality with the original generated profile payload as well as equality with the prepared record's SHA-256 and, when consumed, the original message bytes.

Process test method dispositions against baseline main 2ed497d6, under approved plan section 2.4 and decisions 6774981258/6775792095.

Script inventory: 20 methods; 6 kept, 5 adapted, 9 deleted.

| Baseline method | Disposition and home or removed subject |
|---|---|
| `Given_OwnedChild_When_StopResumeKill_Then_ObservesStoppedProgressExitAndEof` | Deleted: OwnedProducerProcess.RequestStop, ConfirmStopped, Resume, RequestKill and pipe EOF, removed by ADR-021 protocol rule 15. |
| `Given_CommContainsParentheses_When_ParseStartTime_Then_UsesLastDelimiter` | Kept unchanged in B10AbruptProcessTests.cs (including the published-apphost class); options helper uses the new parent identity. |
| `Given_MalformedStat_When_Parse_Then_Rejects` | Kept unchanged in B10AbruptProcessTests.cs (including the published-apphost class); options helper uses the new parent identity. |
| `Given_PreflightErrno_When_Start_Then_FailsBeforeExecutableLaunch` | Adapted: B10AbruptSignalProcessTests.Given_PreflightErrno_When_ChildPreflightRuns_Then_ActualOwnPidAndNullSignalAreRecorded (the child owns its own null-signal preflight). |
| `Given_HandshakeFailure_When_StartupCleanupRuns_Then_PreservesCauseAndActualCleanupObservation` | Deleted: OwnedProducerProcess.CleanupStartupFailure handshake cleanup and its supervisor-issued kill, removed by ADR-021 protocol rule 15. |
| `Given_ExecutableLaunchFailure_When_StartupCleanupFails_Then_PreservesStartFailureName` | Deleted: OwnedProducerProcess.CleanupStartupFailure and supervisor cleanup kill, removed by ADR-021 protocol rule 15. |
| `Given_SecretBearingStartupAndCleanupExceptions_When_HostReportsFailure_Then_EmitsClosedLabelsAndTypesOnly` | Deleted: OwnedProducerProcess.StartupException and removed handshake/kill cleanup-report union, removed by ADR-021 protocol rule 15. |
| `Given_SecretBearingSignalFailure_When_HostReportsFailure_Then_EmitsClosedLabelAndActualObservation` | Adapted: B10AbruptSignalProcessTests, same method name and assertions; renamed exception type. |
| `Given_NativeLoadFailure_When_Preflight_Then_RetainsTypeWithoutFabricatedErrno` | Adapted: B10AbruptSignalProcessTests, same method name and assertions; own-PID preflight. |
| `Given_NativeReturnFailure_When_HostReportsFailure_Then_EmitsActualReturnAndErrno` | Adapted: B10AbruptSignalProcessTests, same method name and assertions; removed retired STOP/CONT input rows. |
| `Given_NativeImportFailure_When_HostReportsFailure_Then_EmitsTypeWithUnavailableReturnAndErrno` | Adapted: B10AbruptSignalProcessTests, same method name and assertions; removed retired STOP/CONT input rows. |
| `Given_UnownedOrChangedTarget_When_Validate_Then_RejectsBeforeSignal` | Deleted: OwnedProducerProcess.ValidateTarget for supervisor signals, removed by ADR-021 protocol rule 15. |
| `Given_SignalFailure_When_Request_Then_RetainsActualErrno` | Deleted: OwnedProducerProcess.RequestSignal for supervisor-issued STOP, removed by ADR-021 protocol rule 15. |
| `Given_NonArmSignal_When_Request_Then_NativeBoundaryIsNotCalled` | Deleted: OwnedProducerProcess.RequestSignal allowed STOP/CONT/KILL set, removed by ADR-021 protocol rule 15. |
| `Given_TracedAmbiguousOrForeignProc_When_ReadIdentity_Then_Rejects` | Deleted: OwnedProducerProcess.ParseIdentity checks authorizing supervisor-issued signals, removed by ADR-021 protocol rule 15. |
| `Given_ProcReadByteBoundary_When_Read_Then_RejectsExcess` | Kept unchanged in B10AbruptProcessTests.cs (including the published-apphost class); options helper uses the new parent identity. |
| `Given_SdkDotnetParent_When_ApphostCommandIsValidated_Then_RejectsWithNamedDetail` | Kept unchanged in B10AbruptProcessTests.cs (including the published-apphost class); options helper uses the new parent identity. |
| `Given_MissingPublishedApphost_When_ApphostCommandIsValidated_Then_RejectsWithDistinctDetail` | Kept unchanged in B10AbruptProcessTests.cs (including the published-apphost class); options helper uses the new parent identity. |
| `Given_ChildWorkerFlag_When_Run_Then_SendsHeartbeats` | Deleted: B10AbruptChildChannel pipe handshake and IPC worker, removed by ADR-021 protocol rule 15. |
| `Given_ChartSelectedPublishedApphost_When_PathGuardRuns_Then_SelectsOnlyThatChildExecutable` | Kept unchanged in B10AbruptProcessTests.cs (including the published-apphost class); options helper uses the new parent identity. |

### Phase and chart methods

Python test method dispositions relative to main 2ed497d6. Authority: decision 6775474492 and plan-check 6776377996 finding 5 apply the plan section 2.4 per-method rule.

Existing methods: 27. Kept/adapted: 25. Deleted: 2.

- Adapted `test_c0_has_only_ordinary_loadgen` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Adapted `test_collection_command_and_copy_failures_record_named_gaps` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Adapted `test_collection_retains_produced_receipt_failure_and_collects_logs` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Retained `test_configmaps_are_immutable_and_cleanup_selects_both` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Retained `test_diagnostic_container_limits_precede_decoding` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Retained `test_every_scientific_entry_rejects_before_work_and_removes_candidate` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Adapted `test_failed_publication_blocks_successor_in_real_trial` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Retained `test_late_release_never_creates_or_launches_successor` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Retained `test_named_admission_failure_is_recorded_once` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Retained `test_outer_unknown_termination_mapping_sets_safety_stop` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Adapted `test_pod_unknown_never_authorizes_successor_and_sets_safety_stop` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Retained `test_prepare_requires_explicit_full_selector_and_confirmation` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Retained `test_produced_candidate_schema_only_relabel_still_rejected` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Adapted `test_produced_checkpoint_descriptor_binds_exact_frame_bytes` -> `test_produced_journal_descriptor_binds_exact_bytes`: Authenticated length/hash/name descriptor checks retained against journal bytes; removed checkpoint frame descriptor.
- Adapted `test_produced_cohort_collection` -> `test_produced_successor_collection`: Successor download and Job-log collection retained; post-release cohort download removed because all first-pod reports are accepted before release.
- Adapted `test_produced_cohort_files_accepted` -> `test_produced_report_files_accepted`: Report descriptor binding and native fatal coverage retained; removed cohort seal inputs.
- Adapted `test_produced_cohort_mutations_rejected` -> `test_produced_report_and_native_mutations_rejected`: Report inventory/hash/length, native counters, capture sequence and failure recording retained; removed seal-count and cohort-status subjects.
- Deleted `test_produced_exclusion_without_journaled_terminal_is_stale`: Kill-selection terminal-exclusion table removed by ADR-021 rule 15.
- Adapted `test_produced_first_and_successor_transport` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Adapted `test_produced_mutations_are_not_accepted` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Retained `test_produced_pod_binding_rejects_disappearance_replaced_uid_and_running` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.
- Deleted `test_produced_second_candidate_without_first_exclusion_is_stale`: Kill-selection candidate priority removed by ADR-021 rule 15.
- Adapted `test_termination_failure_records_its_cause_site` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Adapted `test_trial_order_and_delayed_logs_outside_release_path` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Adapted `test_trial_records_approved_names_for_produced_transport_failures` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Adapted `test_unfinished_first_job_names_its_site` -> same method: Existing subject retained with schema3 evidence and new Job names; protocol-specific inputs retargeted.
- Retained `test_unsuccessful_successor_records_named_trial_failure` -> same method: Existing method body and assertions unchanged; setup now supplies schema3 bytes.

New methods: `test_produced_successor_invocation_payload_binding`, `test_render_hook_selects_only_abrupt_modes`

Chart test files:
- Adapted `test_b10_abrupt_chart.py` existing `main`/`verify`/`invalid_cases`: new mode, args, role, nine policy literals; existing admission/bounds negatives retained. Ordinary modes render with flag absent/false; both old modes rejected with flag true. Four added false/absent schema cases implement decision 6776685705.
- Adapted `test_b10_abrupt_host.py` existing `b10_abrupt_host_acceptance`: new parent flag, original five rejected-input assertions retained, two new Role/JobName rejections. Existing 60 s / 120 s bounds unchanged. No chart test function deleted.

Removed `collect_cohort_files` and its call (plan-check finding 6); all first-pod journal/report bytes are downloaded and checked before successor release. `gap` remains outside PRODUCING_KINDS (finding 7), while any gap still invalidates capture.

### Producer and supervisor methods

| Plan row | Authored test home |
|---|---|
| T-C1, T-C2 | B10AbruptProducerTests.Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup (terminalAfterCut false and true) |
| T-C3 | B10AbruptProducerTests.Given_AllMembersConfirmed_When_ControlCheckpoints_Then_ExposureInconclusive |
| T-C4 | B10AbruptProducerTests.Given_MissingControlTerminal_When_Draining_Then_KillAdmissionIsBlocked (flushHasOutstanding true and false) |
| T-C5 | B10AbruptProducerTests.Given_LastControlTerminalRacesWithCut_When_RunRepeatedly_Then_JournalOrderAndStoredSetAgree (1000 iterations; private CutRaceClock accelerates only the registered release wait, with live ordinary deadlines) |
| T-B1 | B10AbruptSupervisorTests.Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists |
| T-B2 | B10AbruptSupervisorTests.Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists |
| T-B3 | B10AbruptSupervisorTests.Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained (beforeAcceptance true and false) |
| T-R1 | B10AbruptProducerTests.Given_CutOriginalRejected_When_Running_Then_NoKillHasExactCause (three rows) |
| T-S1 through T-S4 | B10AbruptWireProducerTests.Given_ProductionBindingFaults_When_ActualHostValidatesSuccessor_Then_ProducerContinuationNeverStarts (changed_receipt, not_permitted, changed_set, job) |
| section 3.14 export | B10AbruptWireProducerTests.Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts; in-process synthetic producer/process seam, HarnessClock advances real policy120 s, 14 files |

Per-method dispositions below implement approved plan section 2.4 and decisions 6774981258 and 6775792095. Kept/adapted methods retain their surviving subjects; deleted methods exercised only the removed protocol members named in their reasons. The supervisor test file remains as the home of shared-reader and failure-report tests and references no removed supervisor type. The raw descriptor mutations now target the closed journal schema; file-byte and length/hash predicates target the production publisher in B10AbruptPublicationTests.

`B10AbruptProducerTests.cs`:

| Baseline method | Disposition and resulting home or removed subject |
|---|---|
| `Given_OutputDirectoryCollision_When_Initializing_Then_ReportsCaptureGapBeforeConstructingProducer` | kept/adapted: B10AbruptProducerTests.Given_OutputDirectoryCollision_When_Initializing_Then_ReportsCaptureGapBeforeConstructingProducer |
| `Given_ExistingDurableDestination_When_Writing_Then_ReportsCaptureGapAndPreservesExistingBytes` | kept/adapted: B10AbruptProducerTests.Given_ExistingDurableDestination_When_Writing_Then_ReportsCaptureGapAndPreservesExistingBytes |
| `Given_NewDurableDestination_When_Writing_Then_RetainsExactBytes` | kept/adapted: B10AbruptProducerTests.Given_NewDurableDestination_When_Writing_Then_RetainsExactBytes |
| `Given_UnauthorizedLocalAccess_When_Reported_Then_EmitsTypeWithoutPrivateMessageOrPath` | kept/adapted: B10AbruptProducerTests.Given_UnauthorizedLocalAccess_When_Reported_Then_EmitsTypeWithoutPrivateMessageOrPath |
| `Given_FrozenReleaseUtc_When_InnerClockAdvancesOneDay_Then_ResumingExposesAdvancedUtc` | kept/adapted: B10AbruptProducerTests.Given_FrozenReleaseUtc_When_InnerClockAdvancesOneDay_Then_ResumingExposesAdvancedUtc |
| `Given_SuppliedClock_When_ObservingStatistics_Then_ReceivedAtUsesItsUtc` | kept/adapted: B10AbruptProducerTests.Given_SuppliedClock_When_ObservingStatistics_Then_ReceivedAtUsesItsUtc |
| `Given_PreparedAcknowledgementHeld_When_Invoking_Then_NoNativeCallOccurs` | deleted: ADR-021 rule 15 removes pipe acknowledgement |
| `Given_AcceptanceAcknowledgementHeld_When_Invoking_Then_InvocationDoesNotComplete` | deleted: ADR-021 rule 15 removes pipe acknowledgement |
| `Given_CallbackBeforeProduceReturns_When_Invoking_Then_GenuineTerminalPrecedesAcceptance` | kept/adapted: B10AbruptProducerTests.Given_CallbackBeforeProduceReturns_When_Invoking_Then_GenuineTerminalPrecedesAcceptance |
| `Given_NoCallback_When_Invoking_Then_NoTerminalOrPersistenceIsInvented` | kept/adapted: B10AbruptProducerTests.Given_NoCallback_When_Invoking_Then_NoTerminalOrPersistenceIsInvented |
| `Given_OriginalCount_When_InvokingProbe_Then_OnlyNextMemberIsAdmitted` | kept/adapted: B10AbruptProducerTests.Given_OriginalCount_When_InvokingProbe_Then_OnlyNextMemberIsAdmitted |
| `Given_ExistingResend_When_InvokingAnother_Then_GuardRejectsBeforeDurableOrNativeWork` | kept/adapted: B10AbruptProducerTests.Given_ExistingResend_When_InvokingAnother_Then_GuardRejectsBeforeDurableOrNativeWork |
| `Given_PredecessorSequenceMismatch_When_InvokingResend_Then_GuardRejectsBeforeDurableOrNativeWork` | kept/adapted: B10AbruptProducerTests.Given_PredecessorSequenceMismatch_When_InvokingResend_Then_GuardRejectsBeforeDurableOrNativeWork |
| `Given_ProbeCallback_When_Recorded_Then_NoStageWriteUntilMarkTerminals` | adapted: B10AbruptProducerTests.Given_JournalWriterBlocked_When_AnotherCallbackArrives_Then_CallbackEnqueuesWithoutWaiting and Given_ProbeCallback_When_Recorded_Then_WritesNeitherConsoleStream; retired terminal stage replaced by writer-only journal work |
| `Given_TerminalFrame_When_Written_Then_HookRunsOnWriterAfterFrame` | deleted: ADR-021 rule 15 removes child-channel terminal-written hook |
| `Given_ProbeCallback_When_Recorded_Then_WritesNeitherConsoleStream` | kept/adapted: B10AbruptProducerTests.Given_ProbeCallback_When_Recorded_Then_WritesNeitherConsoleStream |
| `Given_ReportAndAcceptanceAcknowledgementsHeld_When_Invoking_Then_ProducedAndSelectedStagesWaitForNextMark` | deleted: ADR-021 rule 15 removes pipe acknowledgement and retired child stages |
| `Given_TerminalStageWriterBlocked_When_AnotherCallbackArrives_Then_CallbackRecordsWithoutWaitingForWriter` | adapted: B10AbruptProducerTests.Given_JournalWriterBlocked_When_AnotherCallbackArrives_Then_CallbackEnqueuesWithoutWaiting |
| `Given_ProductionHostAndQueuedTerminals_When_FirstFrameIsWritten_Then_OnlyItsTerminalStageIsMarked` | deleted: ADR-021 rule 15 removes child-channel terminal-written hook |
| `Given_SynchronousRejection_When_Invoking_Then_NoAcceptedRecordIsWritten` | kept/adapted: B10AbruptProducerTests.Given_SynchronousRejection_When_Invoking_Then_NoAcceptedRecordIsWritten |
| `Given_PreAcceptanceReport_When_Invoking_Then_RawFileAndExactAttributionPrecedeAccepted` | kept/adapted: B10AbruptProducerTests.Given_PreAcceptanceReport_When_Invoking_Then_RawFileAndExactAttributionPrecedeAccepted |
| `Given_OverflowedCounter_When_Invoking_Then_EarlierReportCannotRepairMissingSample` | kept/adapted: B10AbruptProducerTests.Given_OverflowedCounter_When_Invoking_Then_EarlierReportCannotRepairMissingSample |
| `Given_InvalidStatistics_When_Selecting_Then_GuardFails` | kept/adapted: B10AbruptProducerTests.Given_InvalidStatistics_When_Selecting_Then_GuardFails |
| `Given_AvailableCapturedStatistics_When_ReadBack_Then_RejectsObservedTransmissionError` | kept/adapted: B10AbruptProducerTests.Given_AvailableCapturedStatistics_When_ReadBack_Then_RejectsObservedTransmissionError |
| `Given_EmptyAvailableStatistics_When_ReadBack_Then_DoesNotInventASampleCompletenessGate` | kept/adapted: B10AbruptProducerTests.Given_EmptyAvailableStatistics_When_ReadBack_Then_DoesNotInventASampleCompletenessGate |
| `Given_DroppedNativePrefix_When_LaterReportReachesSink_Then_GapRemainsSticky` | kept/adapted: B10AbruptProducerTests.Given_DroppedNativePrefix_When_LaterReportReachesSink_Then_GapRemainsSticky |
| `Given_NativeOversizeOrQueueOverflow_When_Selecting_Then_NoLaterReportRepairsCapture` | kept/adapted: B10AbruptProducerTests.Given_NativeOversizeOrQueueOverflow_When_Selecting_Then_NoLaterReportRepairsCapture |
| `Given_JoinedPostDrainPrefixAndPendingLaterCallback_When_Sealing_Then_EligiblePrefixRemainsComplete` | kept/adapted: B10AbruptProducerTests.Given_JoinedPostDrainPrefixAndPendingLaterCallback_When_Sealing_Then_EligiblePrefixRemainsComplete |
| `Given_AdditionalStableQualifyingBroker_When_Selecting_Then_ExpectedSubsetPasses` | kept/adapted: B10AbruptProducerTests.Given_AdditionalStableQualifyingBroker_When_Selecting_Then_ExpectedSubsetPasses |
| `Given_MissingSourceOrChangedQualifyingSet_When_Selecting_Then_GuardFails` | kept/adapted: B10AbruptProducerTests.Given_MissingSourceOrChangedQualifyingSet_When_Selecting_Then_GuardFails |
| `Given_ReportAtAcceptance_When_Selecting_Then_NoPreAcceptanceSampleExists` | kept/adapted: B10AbruptProducerTests.Given_ReportAtAcceptance_When_Selecting_Then_NoPreAcceptanceSampleExists |
| `Given_TimeoutInDrain_When_RunningCohort_Then_RetriesOnceWithSeparateInventory` | deleted: ADR-021 rule 15 removes timeout cohort |
| `Given_TimeoutCohortCallback_When_CheckingGuards_Then_PossiblyPersistedRequiresPermittedTimeout` | deleted: ADR-021 rule 15 removes timeout cohort |
| `Given_ProbeCallback_When_AdmittingResend_Then_PossiblyPersistedRequiresPermittedTimeout` | kept/adapted: B10AbruptProducerTests.Given_ProbeCallback_When_AdmittingResend_Then_PossiblyPersistedRequiresPermittedTimeout |
| `Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup` | kept/adapted: B10AbruptProducerTests.Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup |
| `Given_ControlArm_When_Sealed_Then_InventoryHasSixEntriesWithMemberSequences` | deleted: ADR-021 rule 15 removes cohort seal |
| `Given_ControlArm_When_Run_Then_FiveSequentialOriginals` | kept/adapted: B10AbruptProducerTests.Given_ControlArm_When_Run_Then_FiveSequentialOriginals |
| `Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly` | kept/adapted: B10AbruptProducerTests.Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly |
| `Given_MissingControlTerminal_When_Draining_Then_KillAdmissionIsBlocked` | kept/adapted: B10AbruptProducerTests.Given_MissingControlTerminal_When_Draining_Then_KillAdmissionIsBlocked |
| `Given_OutstandingMessages_When_DrainBudgetExpires_Then_DiagnosticNamesOutstandingCountAndBudget` | kept/adapted: B10AbruptProducerTests.Given_OutstandingMessages_When_DrainBudgetExpires_Then_DiagnosticNamesOutstandingCountAndBudget |
| `Given_NoReportAfterDrainBoundary_When_WaitExpires_Then_DiagnosticNamesEachSelectionPredicate` | kept/adapted: B10AbruptProducerTests.Given_NoReportAfterDrainBoundary_When_WaitExpires_Then_DiagnosticNamesEachSelectionPredicate |
| `Given_DuplicateTimeoutCallback_When_CheckingGuards_Then_DiagnosticNamesDuplicateCallbackReason` | deleted: ADR-021 rule 15 removes timeout cohort |
| `Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains` | kept/adapted: B10AbruptProducerTests.Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains |
| `Given_ReadinessAroundRelease_When_Replaying_Then_NeverInvokesEarlyOrAfterLateReadiness` | kept/adapted: B10AbruptProducerTests.Given_ReadinessAroundRelease_When_Replaying_Then_NeverInvokesEarlyOrAfterLateReadiness |
| `Given_MissingPersistenceOnGenuineCallback_When_Running_Then_GuardBlocksControl` | kept/adapted: B10AbruptProducerTests.Given_MissingPersistenceOnGenuineCallback_When_Running_Then_GuardBlocksControl |
| `Given_CallbackAfterAcceptance_When_Recording_Then_PreservesTerminalForFinalReconciliation` | kept/adapted: B10AbruptProducerTests.Given_CallbackAfterAcceptance_When_Recording_Then_PreservesTerminalForFinalReconciliation |

`B10AbruptSupervisorTests.cs`:

| Baseline method | Disposition and resulting home or removed subject |
|---|---|
| `Given_NoDurableAcceptance_When_Finalizing_Then_ReportsKillTooEarly` | adapted: B10AbruptProducerTests.Given_CutOriginalRejected_When_Running_Then_NoKillHasExactCause (cross arm all rejected) |
| `Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists` | kept/adapted: B10AbruptSupervisorTests.Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists |
| `Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists` | kept/adapted: B10AbruptSupervisorTests.Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists |
| `Given_InvalidReportBinding_When_AcceptedArrives_Then_FirstBindingFailureWins` | kept/adapted: B10AbruptSupervisorTests.Given_InvalidReportBinding_When_AcceptedArrives_Then_FirstBindingFailureWins |
| `Given_ReportFileAtByteBoundary_When_Bound_Then_ExactLimitIsEnforced` | kept/adapted: B10AbruptSupervisorTests.Given_ReportFileAtByteBoundary_When_Bound_Then_ExactLimitIsEnforced |
| `Given_UnknownArmAlongsideTwoValidArms_When_Bound_Then_RecognizedArmsHaveNoSampleFailure` | kept/adapted: B10AbruptSupervisorTests.Given_UnknownArmAlongsideTwoValidArms_When_Bound_Then_RecognizedArmsHaveNoSampleFailure |
| `Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained` | kept/adapted: B10AbruptSupervisorTests.Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained |
| `Given_CheckpointFlushAtArmDeadline_When_ArmingKill_Then_OnlyOnTimeCheckpointAuthorizesKill` | deleted: ADR-021 rule 15 removes checkpoint deadline |
| `Given_QueuedTerminalDrainAtArmDeadline_When_Checkpointing_Then_LateDrainCannotAuthorizeKill` | deleted: ADR-021 rule 15 removes checkpoint deadline |
| `Given_FullIncomingQueue_When_IncrementalReaderCaptures_Then_RetainsOnlyQueueFullGap` | adapted: B10AbruptJournalTests.Given_TerminalQueueBoundary_When_Capture_Then_OverflowIsSticky |
| `Given_TerminalAvailableBeforeStop_When_ReaderHasNotEnqueued_Then_CheckpointWaitsAndExcludesOriginal` | deleted: ADR-021 rule 15 removes stop/checkpoint synchronization |
| `Given_PartialFrameBufferedAtStop_When_Checkpointing_Then_RejectsWithoutSealingOrResuming` | deleted: ADR-021 rule 15 removes pipe framing at stop |
| `Given_MechanicalCompletionAtTenSecondBoundary_When_Observed_Then_OnlyOnTimeCompletionPasses` | deleted: ADR-021 rule 15 removes signal/reap/EOF protocol budgets |
| `Given_KillSignalRecordFlushAtDeadline_When_Observed_Then_LateCompletionRetainsActualSignalAndFails` | deleted: ADR-021 rule 15 removes supervisor signal journal and deadline |
| `Given_ReapReportsNoExit_When_SupervisorObserves_Then_ExitRemainsUnobserved` | adapted: B10AbruptRealProcessTests.Given_RealSleepingChild_When_ParentWaitBoundExpires_Then_UnknownChildRemainsAliveUntilTestCleanup; pipe EOF variant removed with EOF protocol |
| `Given_EventsRemainOpen_When_EofBudgetExpires_Then_EofIsIncomplete` | deleted: ADR-021 rule 15 removes pipe EOF |
| `Given_TerminalPersistenceTuple_When_SupervisorCaptures_Then_OnlyPermittedTuplePasses` | kept/adapted: B10AbruptSupervisorTests.Given_TerminalPersistenceTuple_When_SupervisorCaptures_Then_OnlyPermittedTuplePasses |
| `Given_CleanupSignalFailure_When_SupervisorAborts_Then_OriginalCauseAndActualCleanupEvidenceSurvive` | deleted: ADR-021 rule 15 removes supervisor abort signals |
| `Given_CleanupReapFailure_When_SupervisorAborts_Then_ReapIsNotMislabelledAsWriterFailure` | deleted: ADR-021 rule 15 removes supervisor abort reap |
| `Given_ExitedChildAtCleanEof_When_SupervisorFails_Then_SkipsGraceAndCleanup` | deleted: ADR-021 rule 15 removes pipe EOF grace |
| `Given_ChildExitsBeforeGraceBoundary_When_CleanEofWaits_Then_RetainsFailureWithoutCleanup` | deleted: ADR-021 rule 15 removes pipe EOF grace |
| `Given_LiveChildAtGraceBoundary_When_CleanEofExpires_Then_CleanupRechecksExitAndAttemptsKillOnce` | deleted: ADR-021 rule 15 removes pipe EOF grace/abort kill |
| `Given_EnclosingCancellationDuringGrace_When_CleanEofWaits_Then_DeadlineFailureStillCleansUp` | deleted: ADR-021 rule 15 removes pipe EOF grace/abort kill |
| `Given_FaultBeforePlannedKill_When_SupervisorFails_Then_CleansUpWithoutFailureReportGrace` | deleted: ADR-021 rule 15 removes supervisor abort signal |
| `Given_PlannedKillBeforeCleanEof_When_SupervisorContinues_Then_UsesPostKillPathWithoutFailureReportGrace` | deleted: ADR-021 rule 15 removes pipe EOF post-kill path |
| `Given_AcknowledgementFlushAdvancesClock_When_AcceptedIsAcknowledged_Then_AckSentFollowsFlush` | deleted: ADR-021 rule 15 removes pipe acknowledgement |
| `Given_LiveChildAtCleanEof_When_GraceWaits_Then_EventStreamEndedRemainsUnwritten` | deleted: ADR-021 rule 15 removes pipe EOF grace |
| `Given_ChildExitsDuringGrace_When_UnexpectedExitIsReported_Then_ElapsedIncludesGraceWait` | deleted: ADR-021 rule 15 removes pipe EOF grace |
| `Given_StartupThrows_When_SupervisorReports_Then_PreservesOriginalAndDoesNotRepeatOwnedCleanup` | kept/adapted: B10AbruptSupervisorTests.Given_StartupThrows_When_SupervisorReports_Then_PreservesOriginalAndDoesNotRepeatOwnedCleanup |
| `Given_ControlTerminalBeforeStop_When_SupervisorRuns_Then_DiagnosticNamesCheckpointAndFacts` | deleted: ADR-021 rule 15 removes checkpoint/stop diagnostics |
| `Given_ControlWithoutTerminal_When_ResumeProgressIsMissing_Then_DiagnosticNamesDeadlineFacts` | deleted: ADR-021 rule 15 removes resume confirmation |
| `Given_InvalidTimeoutSeal_When_SupervisorLoads_Then_DiagnosticStopsAtDecisiveCheck` | deleted: ADR-021 rule 15 removes timeout seal |
| `Given_SignalAndWriterCleanupFailures_When_Reporting_Then_BothCauseDiagnosticsSurvive` | kept/adapted: B10AbruptSupervisorTests.Given_SignalAndWriterCleanupFailures_When_Reporting_Then_BothCauseDiagnosticsSurvive |
| `Given_OversizeFailure_When_Reporting_Then_OriginalSurvivesAndDiagnosticReduces` | kept/adapted: B10AbruptSupervisorTests.Given_OversizeFailure_When_Reporting_Then_OriginalSurvivesAndDiagnosticReduces |
| `Given_OversizeCleanupCause_When_Reporting_Then_BuildReducesItAndWrites` | kept/adapted: B10AbruptSupervisorTests.Given_OversizeCleanupCause_When_Reporting_Then_BuildReducesItAndWrites |
| `Given_TwoUnconfirmedMembers_When_ControlCheckpoints_Then_SelectsLowerOrdinal` | deleted: ADR-021 rule 15 removes checkpoint selection of lowest member |
| `Given_AllMembersConfirmed_When_ControlCheckpoints_Then_ExposureInconclusive` | adapted: B10AbruptProducerTests.Given_AllMembersConfirmed_When_ControlCheckpoints_Then_ExposureInconclusive |
| `Given_DistinctBurstAcceptanceTimes_When_ControlExposureFails_Then_FactsDescribeLastMember` | deleted: ADR-021 rule 15 removes burst/stop facts |
| `Given_FourAccepted_When_Handled_Then_NoStopSignal` | deleted: ADR-021 rule 15 removes burst stop admission |
| `Given_BurstWithLastTerminalAfterStop_When_ControlCheckpoints_Then_BurstLineSeparatesAppendFromGap` | deleted: ADR-021 rule 15 removes burst stderr line |
| `Given_SequenceOutOfOrder_When_Accepted_Then_CaptureGap` | kept/adapted: B10AbruptSupervisorTests.Given_SequenceOutOfOrder_When_Accepted_Then_CaptureGap |
| `Given_InOrderSequencesWithNonIncreasingOrdinals_When_Accepted_Then_CaptureGap` | adapted: B10AbruptProducerTests.Given_OriginalCount_When_InvokingProbe_Then_OnlyNextMemberIsAdmitted |
| `Given_ReportNamesMember1BeforeMember0Accepted_When_Member0Accepted_Then_ReportBindingInvalid` | kept/adapted: B10AbruptSupervisorTests.Given_ReportNamesMember1BeforeMember0Accepted_When_Member0Accepted_Then_ReportBindingInvalid |
| `Given_TwoUnexcludedCandidates_When_Finalized_Then_SelectsLowestOnly` | deleted: ADR-021 rule 15 removes kill selection of one member |
| `Given_ControlSeal_When_Loaded_Then_RequiresSixEntries` | deleted: ADR-021 rule 15 removes cohort seal count |
| `Given_ControlResendPredecessorNotLowestSelected_When_SealLoaded_Then_ControlIncomplete` | deleted: ADR-021 rule 15 removes cohort seal predecessor selection |

`B10AbruptWireProducerTests.cs`:

| Baseline method | Disposition and resulting home or removed subject |
|---|---|
| `Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications` | kept/adapted: B10AbruptWireProducerTests.Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications |
| `Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts` | kept/adapted: B10AbruptWireProducerTests.Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts |
| `Given_TerminalAfterCheckpointBeforeEof_When_Reconciling_Then_NoReplaySelectionIsPublished` | kept/adapted: B10AbruptWireProducerTests.Given_TerminalAfterCheckpointBeforeEof_When_Reconciling_Then_NoReplaySelectionIsPublished |
| `Given_ProductionBindingFaults_When_ActualHostValidatesSuccessor_Then_ProducerContinuationNeverStarts` | kept/adapted: B10AbruptWireProducerTests.Given_ProductionBindingFaults_When_ActualHostValidatesSuccessor_Then_ProducerContinuationNeverStarts |
| `Given_NativeSignalFailure_When_SupervisorAborts_Then_PersistsActualObservation` | deleted: ADR-021 rule 15 removes supervisor STOP signal |
| `Given_ReapOrEofFailure_When_SupervisorRuns_Then_DiagnosticBlocksSuccessor` | adapted: B10AbruptRealProcessTests.Given_RealSleepingChild_When_ParentWaitBoundExpires_Then_UnknownChildRemainsAliveUntilTestCleanup; pipe EOF variant removed with EOF protocol |
| `Given_CreateRefusal_When_Publishing_Then_AmbiguousPreservesCauseWithoutReadbackOrRetry` | kept/adapted: B10AbruptWireProducerTests.Given_CreateRefusal_When_Publishing_Then_AmbiguousPreservesCauseWithoutReadbackOrRetry |
| `Given_OtherServiceCreateFailure_When_Publishing_Then_AmbiguousPreservesCauseWithoutReadbackOrRetry` | kept/adapted: B10AbruptWireProducerTests.Given_OtherServiceCreateFailure_When_Publishing_Then_AmbiguousPreservesCauseWithoutReadbackOrRetry |
| `Given_UncertainCreateOutcome_When_Publishing_Then_AmbiguousPreservesCauseAndDoesNotRetry` | kept/adapted: B10AbruptWireProducerTests.Given_UncertainCreateOutcome_When_Publishing_Then_AmbiguousPreservesCauseAndDoesNotRetry |
| `Given_UnverifiableReadBack_When_Publishing_Then_AmbiguousPreservesCause` | kept/adapted: B10AbruptWireProducerTests.Given_UnverifiableReadBack_When_Publishing_Then_AmbiguousPreservesCause |
| `Given_OversizedReadBack_When_Publishing_Then_PreservesPublicationFailure` | kept/adapted: B10AbruptWireProducerTests.Given_OversizedReadBack_When_Publishing_Then_PreservesPublicationFailure |
| `Given_ChangedReadBack_When_Publishing_Then_FailsWithoutRetry` | kept/adapted: B10AbruptWireProducerTests.Given_ChangedReadBack_When_Publishing_Then_FailsWithoutRetry |
| `Given_AlreadyPublishedObject_When_PublishingAgain_Then_AmbiguousWithoutAnotherWrite` | kept/adapted: B10AbruptWireProducerTests.Given_AlreadyPublishedObject_When_PublishingAgain_Then_AmbiguousWithoutAnotherWrite |



## Continuation corrections and private fixture choice

Historical cause 19 now uses native statistics age relative to producer creation; the real T-N3 host test passes. Historical cause 20 removes the null local Kafka configuration entry. Pinned Confluent sources show that [Config.Set stores the supplied value](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config.cs#L95), the [typed property path removes null](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config.cs#L193), and the [producer rejects null entries](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L794). T-P8 rebinds unrelated report/manifest/termination descriptors while retaining the journal report binding; M10 now reaches and detects that comparison. No expectation or protocol value changes.

The first real-Kafka E1 run then reached a complete journal ending in kill_intent and local exit 137, but the workspace's cgroup namespace/mount mismatch made normal resolution unavailable. It correctly blocked successor; E2 did not run. The private fixture choice preserves that production rejection and supplies the existing parent capture delegates through an internal host test hook only for the real-Kafka, real-time, parent startup-hook mode. It prefers normal capture; only the exact observed `0::/` membership with `/..` mount-root mismatch permits two fixed candidate paths. This is an in-scope private test seam under plan sections 2.1 and 8.2, not a contract, requirement, configuration or production-resolver change.

The fallback requires the observer's own PID in exactly one candidate's domain cgroup. It reads membership, cgroup type and the actual local OOM counter through the same opened directory descriptor, validating membership before and after that counter read. Before/after observations retain actual boot ID and inode, recheck observer identity, and use the existing CaptureAfter/Compare checks. It supplies no synthetic counter or default zero. This proves the fixture's local lookup and observation; it does not prove the production resolver or deployed AKS layout works in this workspace's namespace arrangement.

Pinned Linux sources define [domain cgroup type](https://github.com/torvalds/linux/blob/v6.11/Documentation/admin-guide/cgroup-v2.rst#L845), [cgroup.procs membership, including unsorted/duplicate PID caveats](https://github.com/torvalds/linux/blob/v6.11/Documentation/admin-guide/cgroup-v2.rst#L867), [oom_kill](https://github.com/torvalds/linux/blob/v6.11/Documentation/admin-guide/cgroup-v2.rst#L1398) and [nonhierarchical memory.events.local](https://github.com/torvalds/linux/blob/v6.11/Documentation/admin-guide/cgroup-v2.rst#L1405). Descriptor identity relies on [fdinfo mount/inode fields](https://github.com/torvalds/linux/blob/v6.11/Documentation/filesystems/proc.rst#L1963) and the pinned proc descriptor implementation's [retained file reference](https://github.com/torvalds/linux/blob/v6.11/fs/proc/fd.c#L37) and [path link](https://github.com/torvalds/linux/blob/v6.11/fs/proc/fd.c#L175). The namespace diagnosis follows [cgroup namespace documentation](https://github.com/torvalds/linux/blob/v6.11/Documentation/admin-guide/cgroup-v2.rst#L2748), [namespace-relative path selection](https://github.com/torvalds/linux/blob/v6.11/kernel/cgroup/cgroup.c#L1895) and [kernfs ancestor-path construction](https://github.com/torvalds/linux/blob/v6.11/fs/kernfs/dir.c#L105). These sources support the lookup seam; the successful targeted capture is a local observation.

New `B10AbruptLocalCgroupCaptureTests` methods add 20 parameterized proof cases (counted by script); retained cgroup coverage adds 21, for the 41-case targeted pass above. These are new methods, so no baseline method is deleted or weakened.

| New method | Covered proof |
|---|---|
| `Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` | Actual PID domain membership, boot/inode and local counter before/after. |
| `Given_UnprovenDomainMembership_When_Validated_Then_FailsClosed` | Seven absent, foreign, malformed or non-domain inputs. |
| `Given_UnorderedRepeatedOwnPid_When_Validated_Then_MembershipIsProven` | Documented unsorted/duplicate membership input. |
| `Given_NoOrAmbiguousMemberCandidate_When_Selected_Then_FailsClosed` | Neither or both candidates prove membership. |
| `Given_ExactlyOneMemberCandidate_When_Selected_Then_UsesItsRealCounter` | Either sole candidate selected with its actual counter. |
| `Given_ChangedProofAfterBefore_When_CapturedAfter_Then_Rejects` | Membership, boot, inode, observer identity or counter change. |
| `Given_PathReplacedDuringProof_When_Captured_Then_ProofAndCounterStayOnOpenedInode` | Same descriptor binds membership and actual counter across path replacement. |
| `Given_NamespaceMountMismatch_When_ProductionResolves_Then_StillRejectsUnknownRoot` | Production resolver stays fail-closed; fallback recognition rejects other/ambiguous inputs. |

The fixed 1000-iteration cut race now uses a private CutRaceClock. Its ordinary passage, pacing and one-second deadlines use System time; the statistics reporter no longer advances that clock. Only a registered delay longer than 1 s and at most 120 s that ends at the strictly future journal cut release accelerates to that exact release. It allows less than 1 ms of Task.Delay truncation when matching, and never advances before release. The production 120 s release and 1 s cleanup timeout are unchanged. Normalizing just the fixture construction/report-call substitutions recovers the prior race method byte for byte, preserving all 1000 iterations, concurrency and assertions. One corrected race invocation passed in 107.819 s (runner summary 107.955 s), and the other 60 producer fixture cases passed in 2.327 s. The combined 61 cases have zero failures/skips. At final `1d6b5a38`, the full suite also passes (the fixed 1000 race takes 107.368 s), and fresh M18 detection passes its required behavioral-failure check.

Pinned .NET 10.0.13 sources define [System time-provider timestamps/timers](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/Common/src/System/TimeProvider.cs#L13-L21), [Stopwatch frequency and timestamp](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/Common/src/System/TimeProvider.cs#L83-L97), [System timer implementation](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/Common/src/System/TimeProvider.cs#L160-L188), [Task.Delay timeout validation](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L5725-L5728) and [conversion of TotalMilliseconds to long](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L5779-L5787). This is a private fixture correction within the existing test scope, with no changed enforced check.

At the pre-R1 checkpoint the remaining gates were the final authored documentation/ASCII checks, cold review, final-head CI and hosted code/security reviews. Its host-bridge retention applied only through `1d6b5a38`. R1 is now complete and all 21 mutation rows have applicable coverage at the final freeze; corrected-source full acceptance also passes. Final documentation/ASCII checks, R2 and hosted gates remain pending. The merge waits for Hari because the protected rejection test changes. The approved plan, amendment and every stop rule remain in force.

## Sources and remaining boundaries

The design authority is ADR-021 at baseline `2ed497d6`, as adopted by the approved plan. The source verification also covers the .NET 10 [Environment.Exit remarks](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0#remarks), [.NET Generic Host shutdown](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host#host-shutdown), and the pinned [runtime v10.0.13 Process source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.13/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs). Environment.Exit skips finally blocks but raises ProcessExit; the real-host no-drain test covers the composed lifetime and now passes after correction of historical fixture cause 19. Process.Dispose calls Close, so the parent only disposes an observed-dead child; it does not close a live child.

The plan's inherited source set is [Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-10.0), [Process.WaitForExit](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-10.0), [Process.ExitCode](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.exitcode?view=net-10.0), [DllImport](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute?view=net-10.0), POSIX [kill](https://pubs.opengroup.org/onlinepubs/9799919799/functions/kill.html), [write](https://pubs.opengroup.org/onlinepubs/9799919799/functions/write.html) and [sigaction](https://pubs.opengroup.org/onlinepubs/9799919799/functions/sigaction.html), Kubernetes [emptyDir](https://kubernetes.io/docs/concepts/storage/volumes/#emptydir) and [pod lifecycle](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/), and [Azure Storage consistency](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage). The SIGKILL exit code and survival of flushed journal bytes are local observations, not documented portable guarantees.

Part 2 still owns the separate timeout Job, three-Job orchestration, workflow and committed copy, operations-contract rewrite, and test rejecting diagnostic timeout evidence from scientific admission. The current phase path still has two Jobs, so part 1 does not enable a trial. The accepted operations-document gap persists until part 2. The held offline test owns deployed-image exit encoding and AKS journal/death assumptions before a trial; local tests do not settle them. PR3b classification remains paused and must be revised against schema 3. No workflow dispatch, infrastructure operation, scientific result, expected outcome or verdict change is authorized or performed by this part.

## Earlier complete R1 report

This is the complete authored first-round report, retained before the second review; headings are nested here and curly quotation marks are normalized to ASCII to satisfy repository writing standards. Scratch reproduction paths are historical evidence locations; no raw program output or logs are committed.

## B10 execution part 1 cold review

Review mode: comprehensive
Reviewed revision: `f2c1a6bc61157f2dec40cc79aa2f1aa665244472`
Comparison revision: `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43` (`origin/main`)
Authored tested-code revision: `1d6b5a381ee73c9d62ecde4d6f6decd965514b20`
Patch SHA-256 (diff body): `912a1c489d42c6f9e902c246cb4233cb3da2be7267c5cc72cb0f9adbe883d00e`
Authored receipt: `/tmp/404-cold-r1-receipt.md`
Report: complete

### Scope and governing rules

I read the entire frozen authored diff, the receipt, affected surrounding implementation and tests, and all 47 touched files listed below. The review applies AGENTS.md GOVERNANCE (including scope/deferred work), Code Review Rules and writing standards, docs/agents/pull-requests.md in full, docs/engineering/dotnet.md, docs/engineering/code-comments.md, docs/blueprint.md ADRs, and docs/agents/factory.md in full for the two item files. The charter, frozen ordinary-host rules, diagnostic/scientific distinction, process boundary, evidence binding, test-retention rules and named-file architecture/rejection requirements were checked. These governing files and the skill do not change in the diff; their reviewed branch versions agree with the applicable comparison versions. No fallback authority was needed.

Rubric mapping: every path matches "anything"; chart and phase paths additionally match "charts/, infra/scripts/, scripts/"; all C# paths match ".NET code or tests"; item receipt/state match "items/"; producer configuration and the receipt's configuration claims also received the Kafka-setting inventory/ADR checks. The architecture and protected diagnostic rejection tests were checked against the rules and approved plan that cite them by name. No Terraform, workflow, scenario, results, project, package or assembly-reference change is present.

### Specification comparison

Authority is execution part 1 Revision 6, `items/404/plan-b10-execution-loadgen.md` at `fc1a4f36452b2d302f1d73987e1ba5eb967b3d65`, exact-body SHA-256 `728351add854338a840433ab47f4aad540cd4690ea2fbe0a17cbb01ca898272a`. I read the pinned text and the source comments for approval 6776159640, newest exact-revision plan check 6776377996, amendment 6776685705, and the seven decisions adopted by the approval (6773282644, 6774480762, 6774981258, 6775474492, 6775679271, 6775792095, 6776084683), recognizing authority under factory "Item lifecycle" step 3.

- Plan sections 2.1-2.3: the changed-path and ordinary-host freezes are respected. Program.cs changes only abrupt CLI/environment selection. The chart/schema/phase compatibility extension and one phase render-hook condition are within the later authorized scope. No new project, dependency, workflow, scientific result, scenario expectation or production scientific-verifier predicate is introduced.
- Sections 3.1-3.5, R1-R3/R18-R19: the child journal, bounded callback queue, one-lock durable ordering, cut-set snapshot, report bindings, parent process observer, and retirement of stop/IPC mechanics are implemented. Shared-reader reconciliation is incomplete: prepared-only ambiguity is accepted, as the first finding shows.
- Section 3.6, R4-R7: the five-original control, fixed cut, 120 s release, complete-control gate, cross arm, self-kill and no-kill paths are implemented, but actual invocation pacing and synchronous-rejection control completeness are incorrect. These are implementation defects in the approved policy, not requests to change it.
- Sections 3.7-3.12, R8-R17: the parent observes fixed child identity and death, captures cgroup/OOM evidence, publishes authenticated members, and the successor validates inputs before producer construction, preserves payload/key/sequence with fresh IDs, and applies release timing. The C# parent/successor trust the incomplete shared-reader predicate in the first finding. Cold-read properties in section 8.3 hold for the implemented parent signal boundary, callback enqueue boundary, self-only kill, and absence of producer Flush/Dispose after cross-arm admission.
- Sections 3.13-3.14 and amendment 6776685705: schema 3, the 14 exported members, new abrupt modes, diagnostic flag present/true requirement and four negative render cases, produced-byte bridge, per-collect successor invocation checks, architecture file inventory and protected scientific-rejection intent are implemented. The four apply-in-implementation plan-check requirements are represented in source and tests. The independent Python reader's prepared/accepted final reconciliation is stricter than the C# shared reader, exposing a cross-reader inconsistency.
- Sections 2.4 and 8: the receipt supplies per-method keep/adapt/delete and predicate/mutation maps. Most retained subjects are preserved and the planned obsolete tests are retired; the delivered/error contradiction case is lost during one retained test's adaptation. The existing passing map does not cover the three reproduced behavioral cases below. The obsolete deletion is separately flagged exactly as section 2.4 and AGENTS.md require.
- The unchanged two-Job trial orchestration and temporary superseded operations contract are deliberate, explicitly accepted boundaries of part 1 (plan sections 2.2-2.3; decisions 6774480762, 6774981258 and 6775474492). Part 2 still requires a separate approved plan; their deferred work is not treated as missing part 1 implementation. No scope creep was found.

### Source and validation evidence

I checked the new factual claims against the cited sources rather than memory: Confluent.Kafka v2.16.0 Config.cs/Producer.cs null-entry behavior; Linux v6.11 cgroup-v2, proc, proc-fd, kernfs and cgroup namespace source for the new capture proof; .NET runtime v10.0.13 TimeProvider, Task.Delay conversion and Process.Dispose/Close; Microsoft Environment.Exit and Generic Host shutdown/process API documentation; POSIX kill/write/sigaction; Kubernetes emptyDir/Pod lifecycle; and Azure Storage consistency. The receipt labels measured runtime/kernel/exit observations as local observations. I did not independently rerun its full 9977-test acceptance, 21-mutation campaign, container integration or deployed-image proof; those are authored evidence, not a clean-review substitute.

For the three behavior findings I made a detached scratch worktree at the reviewed revision, added reviewer-only tests, built the LoadGen test project successfully with zero warnings/errors, and ran only those reproductions. The final scratch run has four expected assertion failures proving three distinct defects (two journal inputs), zero runner errors and zero skips in 0.470 s. The strengthened journal input starts with a production-generated complete control and removes only cross original 1's accepted record while repairing structural sequence references; it still produces `valid_control=True gaps= failures=0 successor_permitted=True`. The pacing reproduction prints `invocation_deltas_ticks=0,0,0,0,0`; the synchronous-rejection reproduction prints `exit=3 reason=b10_abrupt_control_incomplete`.

Reproduction source: `/tmp/404-cold-repro/tests/Parchwell.LoadGen.Tests/Abrupt/ColdReviewReproductionTests.cs`. Logs: `/tmp/404-cold-repro-build-2.log`, `/tmp/404-cold-repro-tests-2.log`. Commands: `DOTNET_ROOT=/home/agent/.parchwell/dotnet /home/agent/.parchwell/dotnet/dotnet build tests/Parchwell.LoadGen.Tests -c Release` and the compiled test runner with `-class Parchwell.LoadGen.Tests.Abrupt.ColdReviewReproductionTests`. The authored checkout and frozen inputs were not changed.

### Findings

P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptJournal.cs:227  rule: docs/blueprint.md ADR-021 protocol rules 3, 5 and 14; approved Revision 6 sections 3.5 and 3.11  The shared reader returns success without reconciling every prepared attempt to accepted or synchronous_rejection, allowing ambiguous attempts to disappear from the kill set and successor admission.  Evidence: from a production-generated journal with a complete control, remove only cross original 1's accepted frame and repair sequence/prepared_sequence references; Read returns Gaps=[] and Failures={}, and Classify with matching child/cgroup and observed 137 returns SuccessorPermitted=true. LoadSelectionAsync trusts that snapshot at B10AbruptPublisher.cs:161-164. The independent phase reader correctly rejects the same missing relation at infra/scripts/phase/b10_abrupt.py:678. The shared reader also lacks a final complete-control prerequisite, as the second scratch input demonstrates. Add final journal reconciliation and negative admission coverage before returning a usable snapshot.

P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs:115  rule: approved Revision 6 section 3.6 steps 2 and 5; docs/blueprint.md ADR-021 protocol rule 1  Pacing is based on a timestamp taken before ordinal 0's durable prepared/report writes, so setup latency lets later originals run earlier than their required offsets from ordinal 0's actual invocation.  Evidence: the invocation timestamp is recorded only at line 190, after those writes; a scratch flush seam advancing time 50 ms during ordinal 0 setup yields actual invocation deltas [0,0,0,0,0], instead of at least [0,10,20,30,40] ms. The retained pacing test checks prepared-frame timestamps rather than native invocation timestamps. Anchor pacing to ordinal 0's actual invocation and recheck the deadline after a delay, whose pinned runtime conversion can truncate sub-millisecond time.

P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs:147  rule: docs/blueprint.md ADR-021 protocol rule 3; approved Revision 6 section 3.6 steps 4 and 8  A synchronously rejected control attempt is permanently treated as missing a terminal, even though its durable synchronous_rejection is the attempt's terminal record.  Evidence: the catch at lines 193-196 appends that record and returns without setting ProbeAttempt.Terminal; the unconditional probes.Any(Terminal is null) check then forces b10_abrupt_control_incomplete. With original 1 synchronously rejected, all accepted control originals and the selected resend completed, and Flush returning zero, the scratch production runner exits 3 with that reason and never reaches kill_intent. Rejections of ordinals 0-3 are expressly required to let the arm continue; control completeness must recognize their terminal state.

P1  tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs:156  rule: AGENTS.md Code Review Rules; docs/engineering/dotnet.md Testing; approved Revision 6 section 2.4 and decision 6775792095  Adapting the retained terminal-reconciliation test removes its independent delivered/error consistency assertion by forcing delivered = (code == 0) for every input.  Evidence: comparison revision lines 417-423 included (Local_MsgTimedOut, PossiblyPersisted, delivered=true, permitted=false); the new theory no longer accepts delivered as input, so that retained guard branch at B10AbruptJournal.cs:211 cannot fail this test. This is retained terminal reconciliation, outside the approved obsolete-protocol deletion, and the receipt's per-method adaptation does not preserve the old assertion. Restore an independently supplied contradictory delivered/error case.

P1  tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs:16 (comparison revision, removed line)  rule: AGENTS.md Code Review Rules (flag deleted tests with their code); approved Revision 6 section 2.4  The PR deletes assertions of retired stop/resume/pipe/checkpoint/cohort machinery with the production code they tested; this is the mandatory deleted-test flag.  Evidence: Given_OwnedChild_When_StopResumeKill_Then_ObservesStoppedProgressExitAndEof and its assertions are removed with OwnedProducerProcess; the receipt inventories the other retired methods. These particular retirements are explicitly accepted by decision 6774981258 Q4 as narrowed by decision 6775792095 and the approved plan section 2.4. Answer this flag with those authorities as they require; it is separate from the unapproved retained-test loss above and does not request restoration of the obsolete protocol.

Findings: 5 at P0 or P1

Pre-existing (not blocking):

None newly identified outside the explicitly accepted part 1/part 2 deferrals described above.

### Every touched file

- `charts/parchwell/templates/_helpers.tpl`
- `charts/parchwell/templates/jobs.yaml`
- `charts/parchwell/tests/test_b10_abrupt_chart.py`
- `charts/parchwell/tests/test_b10_abrupt_host.py`
- `charts/parchwell/values.schema.json`
- `charts/parchwell/values.yaml`
- `infra/scripts/phase/b10_abrupt.py`
- `infra/scripts/phase/phase.py`
- `infra/scripts/phase/tests/check_b10_abrupt_producers.py`
- `infra/scripts/phase/tests/test_b10_abrupt.py`
- `items/404/execution-loadgen-receipt.md`
- `items/404/state.md`
- `src/Parchwell.LoadGen/Program.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptCgroup.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptChildChannel.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptDiagnostics.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptJournal.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptOptions.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptParent.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProcess.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptWire.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/ChildCommand.cs`
- `src/Parchwell.LoadGen/Run/Abrupt/OwnedProducerProcess.cs`
- `tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptDiagnosticsTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptFatalCaptureTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptHostRegistrationTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptJournalTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOptionsTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptParentProcessTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessHarness.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptPublicationTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptRealProcessTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSignalProcessTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSuccessorCompletionTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/PublishedApphostStartupHook.cs`
- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`
