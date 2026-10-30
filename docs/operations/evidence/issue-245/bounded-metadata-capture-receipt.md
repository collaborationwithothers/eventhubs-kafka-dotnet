This receipt records the bounded authentication metadata capture PR. It contains no live lifecycle result.

## Execution card

- **Deliverable:** Implement the approved bounded, opt-in SingleActionV2 authentication metadata capture candidate for issue #245, Revision 1, at `/workspace/scratch/245-approved-plan.md`. The source commit is `03a8248a6eec4dc00f73613d80caa4aaee6ebb54`.
- **Authorization:** Hari's current approval is recorded in `/workspace/scratch/245-approval.md` and identifies plan comment `6523043225` (`updated_at: 2026-10-30T12:38:54Z`) and check `6523146669` (`updated_at: 2026-10-30T12:44:30Z`). Apply its exact P2 clarification for the paired cancellation test in `ProbeV2LifecycleTests.cs`. The coordinator's authority refresh recorded current GO in `/workspace/scratch/245-authority-comments.json`; no GitHub write or publication is delegated to this implementation session.
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
- **Review budget:** Per AGENTS.md, “Before opening a pull request” rule 8, the cumulative budget is two comprehensive cold-review passes and two focused correction rounds, with no reset across sessions. The initial comprehensive and focused counts are both zero. If the required review exceeds the remaining budget or focus is unsafe, save the work and hand off to Hari; do not exceed the budget.

## Source verification and predicate coverage

The approved plan and check are the exact sources above. The approved check has no blocking findings; its P2 is applied in the named lifecycle test as a paired disabled/enabled cancellation case, triggered by the first producer's successful delivery callback and followed by cancellation of the test-owned stopping token. The implementation candidate adds no verifier predicate, so verifier boundary-map requirements are not applicable.

The diagnostic predicate map follows plan section 4. Predicate 1 is exercised by absent, exact `false`, exact `true`, and invalid uppercase, numeric, empty, leading-space, trailing-space and newline values; these cases preserve the existing binder tests for required leaves, unknown names, fixed limits and identity. Predicate 2 is exercised by exactly two ordered boundaries, one sampler factory and two calls, with returned `unavailable` and `sample_limit` statuses retained; construction/first-call/second-call errors assert sanitized reasons and no retry. Predicate 3 is exercised by cleanup-incomplete precedence (final sample and session read never occur), no constructed client, first-installed E0 with a later E1, empty/acquisition-only/no-install snapshots, and combined observer/snapshot error precedence. Predicate 4 uses byte-measured values at 262143, 262144 and 262145 UTF-8 bytes, plus non-ASCII padding, oversized fallback, serializer failure, write/flush failure and a manually advanced deadline. No unreachable verifier predicate applies.

## Acceptance

Implementation is present on this branch, but mandatory acceptance is incomplete. The first `./scripts/verify-dotnet.sh` invocation was piped to `tee` before the pinned SDK was installed. It printed `Install .NET SDK 10.0.402 separately, then rerun ./scripts/verify-dotnet.sh.`; the pipeline returned 0 because `pipefail` was not enabled. The attempt is counted as attempt 1. Its captured log is `/tmp/issue245-verify-attempt1.log`.

After installing SDK 10.0.402 under `/tmp/issue245-dotnet`, the second and final whole verification attempt ran the acceptance setup and `./scripts/verify-dotnet.sh` with `pipefail`. Locked restore passed. Solution build failed with exit 1 and 8 errors in the new tests: `ProbeV2LifecycleTests.cs` lines 137/140 (invalid array spread), lines 205/206 and 267 (nullable dereferences), line 219 (CA1826); `ProbeV2AuthenticationMetadataTests.cs` line 71 (static lambda captures `status`) and line 45 (CA1826). After that failed build, two narrow edits were made to the helper test (`Events[^1]`, and removing `static` from the status-capturing lambda); they have not been verified. The other listed lifecycle compile errors remain uncorrected. Full log: `/tmp/issue245-verify-attempt2.log`. The script's report directory is `/tmp/parchwell-dotnet-verification.XDYg9eRi`; acceptance export directory is recorded in `/tmp/issue245-capture-acceptance-dir.txt`.

The approved two whole-command attempts are exhausted. After the stop instruction, `git diff --check` was accidentally run while collecting scope state; it produced no output and passed. This isolated result does not make acceptance complete. The producer helper, Python unit tests, npm documentation tests/check and path-scope comparison remain unrun. No temporary reports or exports are included in the authored diff. No passing acceptance result is claimed.

## Cold review

No cold review has run. Acceptance is blocked by the failed solution build and exhausted verification cap, so the diff is not ready for review or publication. The next session needs to correct the named test compilation/analyzer failures, refresh authority, and continue only under a valid plan exhaustion handoff or further direction from the coordinator. Initial comprehensive/focused counts remain zero; no review was started.

## Hosted review and publication

No hosted review or publication has run. The coordinator owns authority refresh and the single handoff. No PR is authorized from this incomplete acceptance state; required hosted review and final-head CI have not run.
