# B10 final judgement implementation receipt

Implementation is stopped on a test-scope conflict, after the prerequisite audit passed under Hari's clarification. The saved code is an incomplete, uncompiled draft for continuation, not a publishable implementation. No pull request or workflow dispatch was made.

## Execution card

- Deliverable: the final sweep projection and scientific five-repeat judge in approved plan sections 1 to 6.
- Authorization: [Revision 6 approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6849364061), plan `items/404/plan-b10-final-judgement.md` at `07e2f2a3d80d9d0342590919d7a349f38a0d546a`. Its SHA-256 was recomputed before implementation and equals `60e4aabd2f42af6b9413b6b98a7a9dc40b8079da880c4b160faa6329719bfabe`.
- Recognized decisions, in creation order: [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916), [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676), [6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), [6846854445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445), [6847743695](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6847743695), [6849359616](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6849359616), [6854898715](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6854898715), and [6856409628](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6856409628). Each was read in full.
- [Plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6849149203): P2-A changes the second T28 fixture to a timeout render failure with authenticated start metadata; P2-B preserves the problem-1 reason in repeat 3 of T36. Neither test has been implemented yet.
- Mandatory gates and commands: plan section 8, A1 to A6; predicate coverage in section 6; `docs/agents/pull-requests.md` rules 1 to 10; final-head CI and both hosted reviews. Hari merges. Hari dispatches post-merge C5; this session dispatches no workflow.
- Exact remaining acceptance commands: `./scripts/verify-dotnet.sh`; `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `bash infra/scripts/lifecycle/check-offline.sh`; `npm run check:docs`; and `git diff --name-only origin/main...HEAD` with the plan's scripted scope check.
- Earliest normal publication: the tested PR at the first applicable clean cold-review checkpoint under rule 8. This backup is exempt from cold review under rule 12 and must not become a PR.
- Root-cause caps: plan section 9, 20 verification-failure and 20 review-finding root causes, each exhausting to `push -wip and hand off to the next session`. No implementation verification or review has run. No existing test or fixture has changed. The standing fixture cap is 40 cases with the same exhaustion action. The new-target cap is 8 files/shards with that exhaustion action.
- Cold-review budget: comprehensive 0 of 2; focused 0 of 2. Subagents performed prerequisite audit and implementation preparation, not cold reviews. No hosted reviews were requested.
- Process limitation: this execution card was written after the initial unpublished drafts, rather than before implementation as required. No normal push or PR occurred. Continuation must retain that record.

## Source and prerequisite evidence

Branch `codex/404-b10-final-judgement` starts at current main `9b13ce03d916bbd14e8d92445966fcfc09f34cfa`. REST reads confirmed merges and a script checked that all three commits are ancestors of this source:

- Sign-in fix PR 447: `92b81289b8c3fdc28883bb90bd9b56b48d2ba679`.
- Scientific three-Job PR 455: `01eb749da3c949a4ce8388c5a88ba20a39f9b279`.
- Render fix PR 457: `9b13ce03d916bbd14e8d92445966fcfc09f34cfa`.

Before any repository edit, `env -u DOCKER_HOST -u DOCKER_CONTEXT -u DOCKER_TLS -u DOCKER_TLS_VERIFY -u DOCKER_CERT_PATH docker --host=unix:///var/run/docker.sock info` succeeded, reporting server 28.4.0. `docker run --rm hello-world` also succeeded. Neither command reached Azure.

### Acceptance A6

A fresh-context reader scripted the numbered source map below at the exact source commit. The controller inspected its evidence and reran the audit. The script also exercised six lifecycle role/configuration combinations, the exact entry and gap fields, unapplied-job no-op behavior and scientific run-ID boundaries; those checks passed. It is an audit of prerequisite contracts, not the unrun implementation acceptance suites.

| Contract | Source evidence at `9b13ce03` | Result |
| --- | --- | --- |
| A1 C1 sequencing | `infra/scripts/phase/phase.py:1058`; `infra/scripts/phase/b10_abrupt.py:1214-1217`; `phase.py:710-718`; `VerifierHostOptions.cs:64-66` | Matches. Publication acceptance precedes successor release; later Jobs respect the phase stop. |
| A2 C0 output | `VerifyRun/VerifyRunHandler.cs:51-65,157-160,182,194-202`; `Common/Phase/PhaseWireCodec.cs:261-276`; `Common/Results/VerifierResultCodec.cs:207-220,1057-1064`; `B10ProducerObservationsCodec.cs:47-58` | Matches the one-producer nonfinal result and exit-1 receipt. |
| A3 C1 object and receipt | `VerifyRun/Abrupt/B10AbruptVerifyHandler.cs:274-321`; `Common/Phase/PhaseWireCodec.cs:185-193` | Matches the scientific nonfinal envelope and last-write receipt binding. |
| A4 no-object cases | `infra/scripts/phase/b10_abrupt.py:1214,1232-1234,723-730,862-872`; LoadGen `Run/Abrupt/B10AbruptPublisher.cs:86-90` | Admission exclusions match. The plan's object-existence assumption remains an assumption: reaching publication does not guarantee successful writes. |
| A5 names/prefixes | LoadGen `Run/Abrupt/B10AbruptWire.cs:225-228`; `Common/Phase/PhasePaths.cs:10-13` | Matches repeat-aware probe keys and prefixes. |
| A6 lifecycle | `infra/scripts/phase/phase.py:335-339,971-972,1031-1041,1048-1076`; `b10_abrupt.py:1191,1204,1224,1236` | Matches both configurations' fields, gaps and unapplied-job exclusion. |
| A7 readers | `VerifierHostOptionsValidator.cs:226-229`; `AbruptEvidenceCodec.cs:51-61`; `EarlierRepeatEvidenceReader.cs:19-22`; `B10AbruptVerifyHandler.cs:194-197` | Matches repeats 1 to 5 and check 30's earlier-record exclusion. |
| A8 gaps/legacy retention | `infra/scripts/phase/phase.py:1084-1085,27-28,1110-1112,788-789,1540-1541,958-967` | Matches the prerequisite's deliberate judgement gaps and retained legacy calls. |
| A9 three-way gate | `VerifierHostOptions.cs:58-78,123-133`; `infra/scripts/phase/phase.py:954`; `docs/operations/b10-abrupt-contract.md:356-365` | Matches under decision 6856409628: scientific IDs get scientific rules, trial IDs retain trial behavior, other IDs retain two producers. |

The initial A6 script exited 1 only because the pinned A9 sentence incorrectly included the diagnostic trial among two-producer IDs. Decision 6856409628 explicitly clarifies that sentence and decision 6847743695 without a plan revision. The updated audit exited 0; no other A1 to A9 difference was identified. The original failed evidence was retained separately in workspace scratch. Script and output are outside the repository, not committed program output: `/workspace/scratch/404-a6-prerequisite-audit.py` and `.txt`, with the earlier copies suffixed `-before-6856409628`.

### LA-F7 replacement evidence

[Decision 6849359616](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6849359616) replaces the plan's 300-byte/2.6-GiB estimate; that estimate is not used. The evidence below is Hari's supplied Log Analytics evidence, not a new query or measurement by this session. Persistent-workspace identifiers are omitted.

- `AZMSApplicationMetricLogs`, `IncomingMessages` on `document-events`: 2,568 in the 2026-11-06 08:00Z hour and 307,827 in the 09:00Z hour. Legacy scientific run 42189889469 ran 08:44:51Z to 09:39:48Z. Attribution of those hours to the run, about 310,000 records over both phases and about 155,000 per phase, is the control session's inference from timestamps. Diagnostic run 43322351104 had a one-repeat C1 window of 30,945 records, as linked by the decision.
- Container Insights `Perf.memoryWorkingSetBytes`, sampled about once a minute and potentially missing peaks: 574.8 MiB for `b10-42189889469-1-c0-b10-sweep-r27lt` over 3 samples; 487.2 MiB for `b10-42189889469-1-c1-b10-sweep-mrzbv` over 3 samples; 282.9 MiB for `b10-42189889469-1-c1-r4-verify-k46dj` over 1 sample.
- The control session's estimate, dated 2026-11-13, is roughly 2.4 KB per record above about 280 MiB fixed cost, reaching 12Gi near 5 million records, more than 30 times the observed phase volume. This is not measured new-sweep memory use: R20 retains different fields, so it is only a scale guide.
- LA-F7 is accepted by name for planning only and is first exercised in the separately authorized governed diagnostic run. LA-F1 to LA-F6 remain unaccepted for any run. Exceeding memory leaves no projection and holds the phase result; no run is authorized here.

## Scope stop and continuation

The prerequisite merge introduced `infra/scripts/phase/tests/test_b10_scientific.py`. Its `ScientificCases.test_judge_and_candidate_named_gap_remove_stale_candidates`, lines 373 to 390 at the source commit, requires scientific C0 and C1 judge/candidate commands to return 1 without calls and to record the unavailable-final-judgement gap. Plan section 3.6 P4 requires those gaps to cease and explicitly says no test asserts them for science after this PR.

Section 2.1 item 7 lists `test_phase.py` and the conditional `test_b10_abrupt.py`, not `test_b10_scientific.py`. Section 2.3 permits fixture corrections preserving assertions/boundaries or changed expected values for intentionally invalid inputs. This method intentionally tests the old gap on scientific inputs; replacing it with successful final judgement changes that boundary and is not an invalid-input fixture correction. No test assertion was changed.

Required decision: authorize the precise replacement of that obsolete method in the newly introduced test file with coverage of the approved final judge/candidate path, or provide another compliant disposition. This is a plan section 12 scope stop, separate from A9; decision 6856409628 does not cover it.

Saved drafts comprise the projection records/codec, final-sweep path/name/receipt additions, and a partial sweep handler. The handler references not-yet-created types and an enum member, so the backup is not buildable. No tests, build, docs check, cold review or normal publication were attempted after the scope stop. The state file records the continuation. Do not open a PR from the backup branch.

Implementer choices so far: split projection records and codec into two Common files; use three proposed sweep files and up to five Common/Judge files, within the eight-target cap. These are tentative. The partial handler's scenario-loader dependency requires reassessment against existing option requirements; no additional required option or contract change has been authorized or implemented. Predicate map remains the approved section 6; none of its new implementation tests has run.

Models: Codex controller and fresh-context Codex subagents, high effort for subagents. No cold reviewer or hosted reviewer ran.
