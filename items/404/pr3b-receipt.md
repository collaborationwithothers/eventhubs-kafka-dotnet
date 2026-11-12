# PR3b classification implementation receipt

This implements the approved diagnostic-only classification first slice for issue 404. It reads the topic before trial cleanup and publishes a nonfinal observation, with no scientific admission or verdict.

## Execution card

- Deliverable and scope: [approved Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/aedb21c045b1a9aef962bdea955e62475c0a1590/items/404/plan-pr3b-classification.md), sections 2 to 6. File SHA-256 verified as `61e66b7e9be7b6989428713c68b9a53daa2f97c2e234f49950a1a3b575ebc86a`.
- Authorization: [approval 6816963301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816963301) names that commit and hash. [Pointer 6816316853](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816316853) and [check 6816937259](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816937259) identify the same revision. Apply its P2-A through P2-D; P2-E supplies references only.
- Recognized decisions: [resume and trial wiring](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815988248), [scope, output, scheduling and caps](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386), [two-name start line and live assumptions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258), and their cited decisions read in full. Earlier answers retain only the subjects listed in plan section 11.1.
- Implementer: Codex, branch `codex/404-pr3b-classification`; Codex trailer on every agent commit. Hari merges the protected verifier changes. No workflow dispatch, trial, Azure access, results or expected-outcome change.
- Mandatory gates: plan section 8 acceptance commands, applicable cold review under docs/agents/pull-requests.md rule 8, final-head CI and hosted Codex code and security reviews. No C5 target change or mutation dispatch.
- Exact acceptance commands: `./scripts/verify-dotnet.sh`; `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `bash infra/scripts/lifecycle/check-offline.sh`; `npm run check:docs`; `git diff --name-only origin/main...HEAD` checked by script against plan section 2.
- Earliest publication checkpoint: after the mandatory local acceptance commands and qualifying cold review, publish the tested PR and run CI and hosted reviews together.
- Review budget: at most two comprehensive passes and two focused correction rounds across sessions, under docs/agents/pull-requests.md rule 8. Exhaustion or unsafe focus saves `-wip` and hands off to Hari; no reset by a new session.
- Fix caps: plan section 9, 20 verification-failure and 20 review-finding root causes per implementing session; exhaustion `push -wip and hand off to the next session`.
- Source baseline: `a5c2403271a65d0bf53d4747631ff8cebae4ec89`. Plan input formats compared against this main revision. Docker local-socket `info` succeeded (server 28.4.0, vfs) and `docker run --rm hello-world` exited 0 before Docker-based tests.

## Implementer choices and predicate map

Implementation and test choices, boundary coverage, verification outcomes and review revisions are recorded below before cold review. Plan section 6 supplies the required named cases; the implementation adds the remaining independent predicate boundaries without changing enforced checks.

### Rendering choice

The chart's `runtime.b10_abrupt_diagnostic` default is false (`charts/parchwell/values.yaml:3`). The ordinary `Phase.render` invokes Helm directly after building the values (`infra/scripts/phase/phase.py:654`). To preserve plan section 3.5's absent-key schema contract without changing either frozen file, the diagnostic trial supplies a null override through a private verifier rendering helper in its allowed phase file. Helm's official [Values files, Deleting a default key](https://helm.sh/docs/chart_template_guide/values_files/#deleting-a-default-key), read 2026-11-12, states that a null override removes the key from the merged values. The reader schema still rejects false and true; a real render test covers the phase helper and the positive chart fixture supplies null. This is an internal rendering choice, not a changed flag contract.

### Reader and classification choices

- The shared start-line extraction returns a typed bundle; its identity/reference checks and exception text are moved from the ordinary reader without changing its behavior (`VerifyRunHandler.cs:84`, `Abrupt/StartLineEvidenceReader.cs:16`). Existing-mode regressions are checked by the full .NET gate.
- Successor captured artifacts are authenticated by length and hash under check 12, with ledger rows independently read under check 13. Statistics and capabilities content is not judged in this slice, as plan section 3.2 explicitly excludes it. The producer can copy ordinary captured evidence (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs:367`), so check 4 applies to the abrupt envelopes consumed by this reader, not an invented abrupt envelope around ordinary captures. Storage composition retains the existing configured verifier and producer store roles; no extra container-name equality check is added beyond plan section 3.1's fixed options.
- `AbruptClassification.cs:34` joins key, sequence and value hash; roles additionally join instance and attempt. Counts remain complete while only the first configured number of coordinates is retained. `AbruptClassification.cs:57` orders witnesses by the approved attempt IDs and coordinates. Unknown window observations and unauthenticated successor fields remain nullable, including `copies_truncated` (check P2-C).
- The closed first object set can hold at most 20119552 bytes and the successor set at most 604176384 bytes, computed from check 5's per-file bounds. Both are below the per-pod 1073741824-byte ceiling; reaching that aggregate boundary with otherwise-valid closed object sets is unreachable. Individual byte bounds and journal entry bounds have independent cases.

### Item-level authority and timing

The issue event records `ready-for-agent` at 2026-11-07T10:14:55Z and the original `Tool: Codex` comment was created 10 seconds later (computed from the REST timestamps). The current run's direct user instruction explicitly assigns Codex and the matching classification approval also names Codex; the implementation follows that current authorization rather than claiming the original tool comment preceded the label. Parent items 348 and 347 were read with their decision/approval and tool-assignment metadata; their standing permissions do not authorize a merge or dispatch in this run. The pinned plan and approval, not an earlier plan file on this issue, define this run.
