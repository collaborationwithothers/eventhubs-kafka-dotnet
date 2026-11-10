# B10 restart execution part 1: execution receipt

This change replaces the stop/checkpoint/pipe protocol with a producer child that journals and kills itself, a parent that observes and publishes, and a successor that validates the journal and resends unconfirmed attempts. It adapts chart and phase evidence to schema 3 while trial wiring, classification and scientific publication remain separate work. Final-source acceptance and all 21 mutation rows are complete; the final authored documentation scan and mandatory reviews remain pending.

## Execution card

- Deliverable: [execution part 1 Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fc1a4f36452b2d302f1d73987e1ba5eb967b3d65/items/404/plan-b10-execution-loadgen.md), sections 2 to 4, with [amendment 6776685705](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776685705).
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775850681), [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776159640), and the seven decisions adopted by that approval. The latest exact-revision [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776377996) states `Blocking findings: none`; its four `apply in implementation` findings apply.
- Plan commit: `fc1a4f36452b2d302f1d73987e1ba5eb967b3d65`. Exact-body SHA-256, recomputed by `sha256sum`: `728351add854338a840433ab47f4aad540cd4690ea2fbe0a17cbb01ca898272a`. Baseline: `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43`, the merged redesign.
- Branch: `codex/404-execution-loadgen`; backup: `codex/404-execution-loadgen-wip`. This continuation starts from `1b7ef642dcf28fe05d4c58c1a28409d2b84b1f12`, as [handoff 6777482103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6777482103) records. Before implementation resumes, this card records the current task: correct the two identified fixture causes and isolate M10, freeze source, complete the approved acceptance and mutation gates, then obtain the mandatory reviews. The current user brief authorizes that continuation without expanding scope.
- Mandatory gates: plan sections 8 and 9, including the real child/Kafka tests and all 21 manual mutations, the full .NET check, chart/phase checks, scope and ASCII checks, documentation scan, cold review, final-head CI and hosted Codex code/security reviews. Exact commands and their status are below.
- Earliest non-backup publication: the tested, cold-reviewed checkpoint under [pull-request rule 8](../../docs/agents/pull-requests.md). That checkpoint remains pending. The protected verifier rejection test means Hari merges the ready pull request; the implementing session does not merge.
- Cumulative cold-review budget: two comprehensive and two focused rounds, across all sessions. Consumed: zero comprehensive and zero focused; these counts never reset on continuation. Another required review beyond that budget, or unsafe focused review, saves a backup and hands off to Hari. Plan section 11(a) separately caps verification-failure and review-finding root causes at 20 each, with exhaustion `push -wip and hand off to the next session`. The previous run exhausted its verification cap and completed that handoff. This fresh continuation has eight new verification-failure root causes and zero review-finding root causes; the two corrected carryovers are historical causes 19 and 20, not new causes. The historical ledger below remains intact.
- Models: Codex; the underlying model identifier is not exposed. Fresh implementation and verification agents use high effort. Cold review has not started.
- Predicate map: plan sections 8.1, 8.2, 8.2a and 3.14, mapped to concrete tests below. Production scientific-verifier predicates do not change. The protected test retains scientific-admission rejection assertions for the schema-3 diagnostic export.

## Cold-review input record

First round: comprehensive review of the entire authored PR diff, including this receipt and state. Code input revision: `1d6b5a381ee73c9d62ecde4d6f6decd965514b20`; comparison revision: `origin/main` at `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43`. The control session supplies the frozen authored-diff metadata after final receipt edits. No earlier cold-review report exists. Cumulative consumed counts before that round: zero comprehensive, zero focused. The complete report and dispositions belong in the PR self-review record; the final report is not added to its own reviewed receipt.

## Implementation and plan-check dispositions

The child records `prepared`, `accepted`, terminal and native-error observations in `journal.jsonl`, with the journal lock ordering the control `cut` and its stored set. The parent starts one child, observes exit, checks identity and cgroup evidence, classifies termination, and publishes the journal, bound reports, manifest and termination receipt. The successor validates the authenticated inputs before constructing a producer, recomputes the kill set, and resends its members at the release boundary. Stop/resume signalling, pipe framing, checkpoints, cohort seals and kill-selection machinery are removed.

The schema amendment adds one adjacent `allOf` condition requiring a present, true diagnostic flag for either new abrupt mode. The chart tests add the four false/absent flag rejection cases. This resolves the scope conflict recorded before implementation resumed; the amendment leaves every other freeze and stop rule in force.

The exact-revision check's findings are disposed of as follows. The verification sections below distinguish preserved previous-run observations from completed final-source acceptance.

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

Final source freeze: `1d6b5a381ee73c9d62ecde4d6f6decd965514b20`; all applicable local acceptance commands and 21 mutation rows are complete; the final authored documentation scan and reviews are pending. Prior cgroup freeze: `9e17e201752def19fbcf35d20d6aa6f5d0f68eea`. The first continuation freeze was `1e0719c6b550e9e92c43654db955610539669b5a`; the cgroup freeze adds only the private capture fixture and its proof tests, and the final freeze changes only the cut-race clock fixture. Commands run in isolated detached worktrees, with `DOTNET_ROOT=/home/agent/.parchwell/dotnet` and that SDK plus `/tmp/parchwell-tools` on PATH. Docker tests use the local Unix socket. Program output remains outside the repository.

| Command or gate | Current evidence |
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
| Cold review; final-head CI; hosted Codex code/security reviews | Pending; zero comprehensive and zero focused cold rounds consumed. No pull request exists. |

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

New verification-failure root causes: eight, counted from the shared continuation ledger below. Known carryovers: historical causes 19 and 20; both corrections are applied. T-N3, corrected E1/E2 and the full fixed-race test pass on final source. M10 input isolation is verified by its caught mutant, not an additional measured failure cause. New review-finding root causes: zero. Each category retains the plan section 11(a) cap of 20; exhaustion is `push -wip and hand off to the next session`. Repeat failures from an existing root cause are not counted again. Cold-review counts remain cumulative at zero comprehensive and zero focused.

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

Remaining gates are the final authored documentation/ASCII checks, cold review, final-head CI and hosted code/security reviews. The host bridge retains its pass at `9e17e201` because the final change affects only the private race fixture; host/export/default-fixture behavior is unchanged. The merge waits for Hari because the protected rejection test changes. The approved plan, amendment and every stop rule remain in force.

## Sources and remaining boundaries

The design authority is ADR-021 at baseline `2ed497d6`, as adopted by the approved plan. The source verification also covers the .NET 10 [Environment.Exit remarks](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0#remarks), [.NET Generic Host shutdown](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host#host-shutdown), and the pinned [runtime v10.0.13 Process source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.13/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs). Environment.Exit skips finally blocks but raises ProcessExit; the real-host no-drain test covers the composed lifetime and now passes after correction of historical fixture cause 19. Process.Dispose calls Close, so the parent only disposes an observed-dead child; it does not close a live child.

The plan's inherited source set is [Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-10.0), [Process.WaitForExit](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-10.0), [Process.ExitCode](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.exitcode?view=net-10.0), [DllImport](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute?view=net-10.0), POSIX [kill](https://pubs.opengroup.org/onlinepubs/9799919799/functions/kill.html), [write](https://pubs.opengroup.org/onlinepubs/9799919799/functions/write.html) and [sigaction](https://pubs.opengroup.org/onlinepubs/9799919799/functions/sigaction.html), Kubernetes [emptyDir](https://kubernetes.io/docs/concepts/storage/volumes/#emptydir) and [pod lifecycle](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/), and [Azure Storage consistency](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage). The SIGKILL exit code and survival of flushed journal bytes are local observations, not documented portable guarantees.

Part 2 still owns the separate timeout Job, three-Job orchestration, workflow and committed copy, operations-contract rewrite, and test rejecting diagnostic timeout evidence from scientific admission. The current phase path still has two Jobs, so part 1 does not enable a trial. The accepted operations-document gap persists until part 2. The held offline test owns deployed-image exit encoding and AKS journal/death assumptions before a trial; local tests do not settle them. PR3b classification remains paused and must be revised against schema 3. No workflow dispatch, infrastructure operation, scientific result, expected outcome or verdict change is authorized or performed by this part.
