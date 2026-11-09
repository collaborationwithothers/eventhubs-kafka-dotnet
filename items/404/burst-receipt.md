# Burst execution receipt

This pull request implements the approved burst protocol in both probe arms. It keeps the diagnostic path inactive for science and resends one selected original per arm. No live trial is authorized.

## Execution card

- Deliverable: PR-B items 1-9, existing-test edits 1-11 and the tests-to-add table of [Revision 5.1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741960293).
- Authorization: [PR-B approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6743971150). The [appendix](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741953375) is part of the plan. Both exact GitHub body hashes and timestamps matched the approval before implementation: plan `54d27d8b010e45ceff9d649e2bcd6f810daf7026d967a5587ce47cd00cb07f98`, updated `2026-11-09T14:03:09Z`; appendix `57945a00f987b335a8e198721cb73915c28505458fe5f7408e3f0dd28587299b`, updated `2026-11-09T14:02:50Z`. No normalization or added newline was hashed.
- Predecessor: [design PR 415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/415), merged at `07c1aa54b1c6ce39ce2553f4eab41d891258f728`; this is the source commit.
- Mandatory gates and publication checkpoint: the plan's PR-B gate. Publish after local acceptance commands 3-7 and 10 and applicable cold review; acceptance commands 8-9 run only in PR CI. Hosted Codex code and security reviews and final-head CI are separate mandatory gates before merge.
- Verification: the exact commands in the plan's acceptance list, repeated below with their results. Commands 8 and 9 are CI-only. No workflow dispatch or sixth trial.
- Budget: plan's caps and exhaustion actions apply: 20 verification-failure root causes, 20 documentation-warning root causes and 20 review-finding root causes; exhaustion saves `-wip` and hands off to the next session. Cold review: at most two comprehensive and two focused rounds cumulatively; exhaustion or unsafe focus saves `-wip` and hands off to Hari under AGENTS.md rule 8. No new scope is added for P2 hardening.
- Models: Codex author; implementation subagents GPT-6.1 Sol, high effort. Cold review recorded below.

## Specification and predicate map

The approved plan and appendix are pinned above; the [Revision 5.1 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741980175) has no blocking findings or apply-in-implementation corrections. The [PR-A approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6741989166) approved only design; PR-B uses the later explicit approval. The design authority is merged ADR-021 protocol rules 1, 5-8 and 15. No later decision changes this implementation at the initial authority read.

Plan coverage and test results will be recorded after implementation. The affected selection predicates are mapped before cold review:

- Burst admission: four accepted originals do not stop; five ordered originals stop; out-of-order originals reject. An excess original rejects. The tests exercise first-report binding separately from ordering.
- Checkpoint unconfirmed set: empty is exposure-inconclusive; two members preserve invocation order; five members remain candidates. There cannot be more than five accepted originals at a checkpoint because admission rejects excess and the trigger requires the ordered complete burst.
- Final selection: one lowest unexcluded candidate, including post-death terminal exclusion. Successor input accepts candidate counts 1-5 and sequence 0-4, retains candidate equality and rejects invalid exclusions or a later eligible member. Empty selection remains invalid; existing malformed-input tests cover the unchanged bounds.
- Control seal: six entries load, two reject; the resend predecessor must be the first saved selected member. The valid six-entry fixture retains genuine terminals and matching ledger rows so bookkeeping cannot mask the predecessor check.
- Release: `[a, b]` chooses `a`; empty rejects. Producer inventory preserves all five original sequences and the selected resend sequence.
- Produced-byte phase checks: the exporter supplies actual burst bytes; a rebound later-candidate selection rejects as `selection stale`, with unrelated publication bindings valid.
- Burst diagnostics: five members maximum; absent ticks use -1; a terminal appended after stop has flag 0 while earlier terminals have flag 1. The manual-clock test separates the accepted append from the ack-to-stop gap.

## Verification

Commands run from the repository root with SDK 10.0.402. The source commit is `07c1aa54b1c6ce39ce2553f4eab41d891258f728` plus the authored changes in this pull request; generated outputs stay outside Git.

1. `dotnet restore Parchwell.slnx --locked-mode`: exit 0.
2. `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-restore`: initial analyzer failures CA1854 and CA1861 corrected without suppressions. The successful build reported zero warnings and errors.
3. `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`: initial run 450 total, 442 passed, 7 failed, 1 skipped. Failures were in new fixtures: incomplete closed-wire fixtures and identity serialized without wire options. A second run left four incomplete cohort-inventory fixtures; these were corrected to carry the production instance/cohort members. Final result: exit 0; 450 total, 449 passed, zero failed, one skipped. The existing published-apphost test requires acceptance command 9 in CI and remains unchanged.
4. `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT="$W" dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`, with a fresh scratch `W`: exit 0; 8 passed, no failures or skips.
5. `python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py "$W"`, on command 4's bytes: exit 0; 11 produced-byte tests passed and the required `b10_abrupt_produced_bytes_checked` marker appeared.
6. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`: exit 0; 12 tests passed.
7. After `dotnet build tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release --no-restore` passed, `PARCHWELL_REPOSITORY_ROOT=$PWD dotnet test --project tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`: exit 0; 1 passed.
8. `./scripts/verify-dotnet.sh`: CI only, not run locally.
9. `bash charts/parchwell/tests/check-loadgen-host.sh`: CI only, not run locally.
10. `npm run check:docs`: initial scan exit 0, eight advisory warnings in unchanged files. The staged receipt scan also exited 0 with the same eight warnings, all in unchanged files. They stand as existing advisory findings outside this PR.

Verification root causes used: 5/20 (CA1854, CA1861, incomplete closed-wire fixtures, resume identity serializer, an MSBuild child-node exit). The MSBuild failure reported MSB4166; its suggested diagnostic directory was absent and the observed cgroup OOM counters were zero, so the cause is unknown. The exact build command was rerun after that failure. No production behavior fix was needed after test execution. Documentation-warning roots in changed files: 0/20. Existing-test adaptations are the plan's exact count/shape changes, including the control outcome replacement; assertions were not loosened or removed.

The confirm-by-running values all matched in the initial suite: the two exact diagnostic stage lists, accepted count 5, six control entries and missing-terminal counts, cancellation counts 6 and 8, and burst tick differences 30000, 0 and 10000 with flags `[1, 1, 1, 1, 0]`. Other existing supervisor RunAsync tests passed. No expected-value difference was observed. One fixture interpretation is necessary: prepared frames contain no `payload_base64`; the new stale-selection tests take candidate identity, sequence, length and hash from prepared and retain the existing selection's identical shared arm payload bytes.

## Cold review

Mode: initial comprehensive review, against `07c1aa54b1c6ce39ce2553f4eab41d891258f728`. Input: the committed branch HEAD containing this receipt and its frozen full diff; the reviewer records the exact SHA in the complete report. The review uses the repository-owned repo-review skill in a fresh context. Cumulative budget including this pass: comprehensive 1/2; focused 0/2. Earlier diagnostics and design pull requests have separate budgets; this PR-B had no earlier cold review. The complete report and dispositions go in the PR self-review record rather than in the report's own reviewed receipt.

Author scope check: PR-B items 1-9 are implemented in their listed members; existing-test edits 1-11 and all rows in the tests-to-add table are covered. The only authored paths are the 15 PR-B allowlisted files. No results, expected outcomes, workflows, projects, dependencies, scientific verdicts or client settings changed. The blueprint ADRs, .NET engineering rules, governance and merge rule were reread against this scope before publication.
