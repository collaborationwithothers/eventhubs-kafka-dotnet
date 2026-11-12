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

## Verification and review

Implementation stopped under plan section 12 before integration or verification. Partial files are saved only as work in progress; they are not a tested implementation and must not be opened as a pull request. Program output and scratch files stay outside the repository at `/workspace/scratch/404-pr3c/`.

### Scope conflict requiring Hari's decision

Plan section 2.2 freezes every existing assertion except section 2.3. New checks 21, 22 and 26 conflict with preexisting success expectations that section 2.3 does not authorize changing:

- `B10AbruptVerifyCoverageTests.Given_RehashedSuccessorManifestEnvelope_When_HostRuns_Then_OnlyExactDiagnosticStructureAuthenticates`, the `opaque-inventory` subcase, sets `instance_id` and `cohort` to `not-reconciled`, `inventory_count` to -1, inventory empty and bookkeeping false, then asserts no problem. Check 26 must reject this input. This alone makes the contract and frozen assertion impossible to satisfy together.
- `B10AbruptVerifyCoverageTests.Given_EnvelopeDepthBelowAtOrAboveCodecLimit_When_HostRuns_Then_OnlyDepthAtMost32Authenticates` replaces the termination child identity object with nested arrays and expects no problem for depths 31 and 32. Check 22 requires the authenticated child fields.
- `B10AbruptVerifyBoundaryTests.Given_JournalCountOrFramingBoundary_When_HostRuns_Then_OnlyCompleteBoundedSequenceAuthenticates` fills the journal with native errors whose capture sequence starts at the total journal count and leaves termination counts unchanged, then expects no problem for record counts 4095 and 4096. Check 21 requires capture sequence 1..n and matching termination counts. The journal-byte padding in the same file uses the same construction.

Recommendation for a revised plan: authorize these existing tests to reflect the newly enforced semantics, preserving boundary coverage with otherwise valid inputs and retaining dedicated invalid-input rejection cases. Do not weaken the new checks or silently delete existing coverage. The plan revision, independent check where required, and Hari's approval must precede continuation.

### Saved partial implementation

- Timeout reader and local publication acceptance files are authored but unbuilt and unintegrated.
- Abrupt evidence records/reader changes and classification fields are incomplete. `AbruptReconciliation.cs` contains partial checks 21 to 27; guard check 28 is not implemented. No build establishes whether these partial APIs compile.
- Trial recognition, codec count, required options, scenario DI and phase metadata changes are partial. Chart wiring and new tests were not written.
- Handler integration, documentation contract changes, component fixtures/tests, exports, live-byte replay and acceptance gates remain outstanding.

### Commands and ledgers

- Managed local Docker `info`: passed before edits. Pinned-plan SHA-256 assertion: passed after fetching the plan branch.
- LoadGen test-project build: canceled by the control session when the scope stop was identified. No test or export ran. Its cancellation is not a verification failure.
- A6 artifact metadata read succeeded. Download through `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13945847489/zip` failed with HTTP 403 at the redirected artifact host. No live bytes were obtained or replayed. No claim is made about the checks on live bytes; this is an additional unresolved gate, not an A6 failed-check observation.
- Verification-failure root ledger: `artifact-download-forbidden` only. The scripted count is 1 of policy cap 20. Review-finding roots: 0 of 20. The scope stop is independent of these budgets.
- Cold reviews started/completed: 0 comprehensive, 0 focused. No hosted review requested; no PR, merge or workflow dispatch.
- No claim that Event Hubs acknowledgements imply watermark visibility was added. The plan labels that inference unverified; its trial-only assumption acceptance does not establish an official capability guarantee.


- Pre-backup refresh: issue and parent comment ID/update sets unchanged; no added, edited or deleted comments. Local whitespace check passed. No existing test assertion or protected frozen path was changed. Backup publication uses AGENTS.md rule 12, not a PR publication checkpoint.
