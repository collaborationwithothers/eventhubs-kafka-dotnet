This receipt records the bounded authentication metadata capture PR. It contains no live lifecycle result.

## Execution card

- **Deliverable:** Implement the approved bounded, opt-in SingleActionV2 authentication metadata capture candidate for issue #245, Revision 1. The approved [plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6523043225) is checked against source commit `03a8248a6eec4dc00f73613d80caa4aaee6ebb54`.
- **Authorization:** Hari's approved [plan approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6523319333) identifies plan comment `6523043225` (`updated_at: 2026-10-30T12:38:54Z`) and [check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6523146669) (`updated_at: 2026-10-30T12:44:30Z`). The resumed execution is authorized by the latest [continuation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6524183975), following the [saved-work handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6523517474). Apply the exact P2 clarification in the check. Current authority refresh is recorded in the coordinator's authority snapshot; this implementation session does not publish or write to GitHub.
- **Mandatory gates:** Implement only the eleven paths in plan section 3; preserve scientific wire records and existing assertions; include all plan section 5 cases plus the P2 cancellation pair; complete every plan section 7 offline acceptance command within two whole-command attempts; pass path-scope comparison and `git diff --check`; keep reports and exports out of the diff. No live Azure, credential/token-content access, manual smoke, workflow dispatch, or deferred resolver/authentication work.
- **Exact verification commands:**

  ```bash
  capture_acceptance_dir=$(mktemp -d /tmp/issue245-capture-acceptance.XXXXXXXX)
  export PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY="$capture_acceptance_dir/actor"
  export PARCHWELL_V2_CONTRACT_EXPORT="$capture_acceptance_dir/judge"
  export PARCHWELL_PROBE_V2_METADATA_FIXTURE_DIRECTORY="$capture_acceptance_dir/metadata-actor"
  ./scripts/verify-dotnet.sh
  python3 infra/scripts/phase/tests/check_probe_v2_producers.py "$PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY" "$PARCHWELL_V2_CONTRACT_EXPORT" "$PARCHWELL_PROBE_V2_METADATA_FIXTURE_DIRECTORY"
  python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_probe_v2.py'
  npm ci --ignore-scripts
  npm run test:docs
  npm run check:docs
  git diff --check
  ```

- **Earliest authorized publication checkpoint:** Once the exact acceptance gates pass and the complete authored diff is frozen for cold review, the coordinator may handle publication under the repository rules. This session will not push, open a PR, or write to GitHub.
- **Review budget:** Per AGENTS.md, "Before opening a pull request" rule 8, the cumulative budget is two comprehensive cold-review passes and two focused correction rounds, with no reset across sessions. Comprehensive and focused counts are both 0. If the required review exceeds the remaining budget or focus is unsafe, save the work and hand off to Hari; do not exceed the budget.

## Source verification and predicate coverage

The approved plan and check are the exact sources above. The approved check has no blocking findings; its P2 is applied in the named lifecycle test as a paired disabled/enabled cancellation case, triggered by the first producer's successful delivery callback and followed by cancellation of the test-owned stopping token. The implementation candidate adds no verifier predicate, so verifier boundary-map requirements are not applicable.

The diagnostic predicate map follows plan section 4. Predicate 1 is exercised by absent, exact `false`, exact `true`, and invalid uppercase, numeric, empty, leading-space, trailing-space and newline values; these cases preserve the existing binder tests for required leaves, unknown names, fixed limits and identity. Predicate 2 is exercised by exactly two ordered boundaries, one sampler factory and two calls, with returned `unavailable` and `sample_limit` statuses retained; construction/first-call/second-call errors assert sanitized reasons and no retry. Predicate 3 is exercised by cleanup-incomplete precedence (final sample and session read never occur), no constructed client, first-installed E0 with a later E1, empty/acquisition-only/no-install snapshots, and combined observer/snapshot error precedence. Predicate 4 uses byte-measured values at 262143, 262144 and 262145 UTF-8 bytes, plus non-ASCII padding, oversized fallback, serializer failure, write/flush failure and a manually advanced deadline. No unreachable verifier predicate applies.

## Acceptance

Hari's continuation decision above authorizes this same approved plan after the required failure handoff. It preserves the cap of two whole-command verification attempts for resumed execution and does not turn earlier failures into passes. Attempt 1 had stopped before the pinned SDK was installed; its captured output is `/tmp/issue245-verify-attempt1.log`. The prior attempt 2 failure log `/tmp/issue245-verify-attempt2.log` records locked restore passing and the solution build failing with eight named new-test compiler/analyzer errors. The corrected cases were then targeted before resumed full verification.

Targeted debugging used SDK 10.0.402 at `/tmp/issue245-dotnet` and the existing Docker daemon (server 28.4.0; local-socket health check passed). The first filtered project run compiled and ran `Given_CancellationAfterFirstDelivery`, `Given_FirstAndLaterInstalls`, and `Given_TwoSamples`; the two helper cases passed. The cancellation case initially asserted one total capture row, while the approved five-slot capture has one attempted repeat and four `not_attempted_cleanup` slots. After correcting that assertion, its targeted rerun passed (1 passed, 0 failed, 0 skipped). The first full resumed verification attempt used `/tmp/issue245-capture-acceptance.prw8pOi1` and `/tmp/parchwell-dotnet-verification.RQcxqxgp`. Restore, solution build, XML inventory consistency, architecture (12), Enrichment (56), Kafka (460) and LoadGen (671/672) gates ran; the only failure was the five-client test expecting 5 sampler calls instead of 10, since each of its five metadata rows takes two samples. The assertion was corrected to 10 and that named test passed in a targeted rerun (1 passed, 0 failed, 0 skipped). Later gates were not reached in this attempt.

Resumed whole-command attempt 2 used export directory `/tmp/issue245-capture-acceptance.Bc2uaXFB`, full verification report directory `/tmp/parchwell-dotnet-verification.GJeqvKwB`, and command log `/tmp/issue245-resumed-attempt2.log`. `./scripts/verify-dotnet.sh` passed: locked restore, Release solution build, Kafka XML consistency, architecture (12 passed), Enrichment (56), Kafka (460), LoadGen (672), Scenarios (239), Verifier (4252), report validation and warning-as-error control all passed, with no failed, skipped, not-run or runner-error tests. A scripted audit of the fresh LoadGen XML report found all 19 new test methods producing 24 case rows, including the exact P2 cancellation case and theory rows, present and passing.

The producer transport helper initially rejected the existing scientific key `metadata_partitions`; after narrowing the check to wire root and repeat fields so Kafka setting names remain valid, the helper passed on actual attempt-2 exports with exactly `{"probe_v2_actual_producer_transport_valid":true}`. Python discovery first exposed the missing Helm prerequisite (49 Helm-render subcase errors); the pinned Helm 4.3.1 binary was then extracted from the documented image digest in `/tmp/issue245-tools/helm`, without running the container. Python discovery passed all 27 tests. `npm ci --ignore-scripts` passed, `npm run test:docs` passed all 28 tests, `npm run check:docs` passed, and `git diff --check` passed. The exact 11-path scope comparison against base `03a8248a6eec4dc00f73613d80caa4aaee6ebb54` passed. The final docs scan reports no issues in either changed document and eight advisory warnings across unchanged documents; the coordinator records the required single blanket unchanged-file warning disposition in the PR description. Logs, reports, exports and the extracted binary remain outside the repository.

## Cold review

No cold review has run. Offline acceptance, the exact eleven-path scope check and document checks pass, so the authored diff is ready for the coordinator to freeze and review. Intended first review: comprehensive pass 1 of the full authored diff, with comparison revision `03a8248a6eec4dc00f73613d80caa4aaee6ebb54` and frozen implementation source revision `de81d0f02a2a343b67cbbf047d02ac7ed69c2dd4`; the review input includes this final receipt preparation. The coordinator records the exact reviewed head in the PR self-review record. This review has not started; cumulative comprehensive/focused counts are 0 / 0. The coordinator owns publication and review coordination.

## Hosted review and publication

No PR, hosted review or publication has run. The coordinator owns authority refresh, GitHub publication and the final handoff. Required hosted review and final-head CI have not run.
