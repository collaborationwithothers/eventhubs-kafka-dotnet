# PR 115 round 16 review dispositions

[Decision 6339066483](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6339066483)
authorizes one bounded round for the five findings in the second completed
external review of head `f79c74cf23f5d0a1d30b399728f265c82208e676`.
Four findings are repaired and focused local verification passes. The start-line
finding is answered against the explicit approved contract. Exact repaired-head
CI and external review are recorded in the PR after push.

The original [decision](raw/review-16-fixes/continuation.json),
[review](raw/review-16-fixes/external-review.json),
[findings and existing replies](raw/review-16-fixes/review-comments.json),
[Revision 2 plan](raw/review-16-fixes/plan-revision-2.json) and
[later issue decisions](raw/review-16-fixes/issue-114-comments.json)
are retained without rewriting their bytes. The full review-comment response
contains 19 comments, below the REST page limit of 100.
[The producer record](review-16-producers.json) gives each original command,
source commit and byte-preserving copy where an earlier scratch capture is used.

| Finding | Severity | Disposition and verification status |
|---|---|---|
| [4347081448](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/115#discussion_r4347081448), start-line comparison | P1 | `Not valid:` against the approved plan's explicit mode boundary; source evidence below. No new start-line scenario check. |
| [4347081459](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/115#discussion_r4347081459), swept cleanup receipt recovery | P1 | Fixed: a fresh sweep closes uniquely registered aged cleanup after rechecking empty state, preserving ownership and incomplete evidence. Failure/replay and unsafe-generation regressions pass. |
| [4347081470](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/115#discussion_r4347081470), gate wrapper lifetime | P1 | Fixed: outer command uses declared 90-second gate job budget around the 60-second verifier lifetime. Workflow budget contracts pass. |
| [4347081480](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/115#discussion_r4347081480), shared admission deadline | P2 | Fixed: observation retains the shared 180-second deadline without a 60-second reserve, plus a local bound for direct callers. Boundary and cancellation cases pass. |
| [4347081492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/115#discussion_r4347081492), immutable export replay | P1 | Fixed: fresh gate/candidate replay reads immutable export originals with manifest, ledger hash, source, identity and ordinal checks. Missing/tampered/partial evidence rejects or leaves counts null. |

## Start-line comparison disposition

`Not valid:` the checked Revision 2 plan's **Images and generated-value mapping**,
paragraph **Mapping is exact**, explicitly states: "Start-line does not perform
scenario equality because its existing mode does not resolve the scenario;
verify performs the verifier-context equality before its Kafka session."
The preceding paragraph assigns compatibility validation to LoadGen and
**Verifier verify**, and also requires the chart to supply the optional environment
key. Supplying that key to start-line does not amend the explicit mode exception.
The [clean plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332608289)
approved this revision. The retained later decisions through 6339066483 do not
amend that exception. The retry amendment changes a separate durable-store rule.

At the reviewed head, [Program.cs](../../../../src/Parchwell.Verifier/Program.cs)
returns after registering start-line at lines 110-114, before scenario services
at lines 121-124. [VerifyRunHandler.cs](../../../../src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs)
resolves and compares at lines 68-74 before native session creation at line 94.
The [operational contract](../../verifier-contract.md#optional-deployment-inventory-and-completion)
states the same boundary. Existing
[comparison tests](../../../../tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs)
at lines 92-100 keep the start-line comparison path inactive, while the
real-resolution and bad-byte verify cases at lines 103-145 cover C0/C3 and
failure before native construction. Existing
[composition tests](../../../../tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs)
at lines 499-524 require no scenario loader for start-line. The existing cases also pass in the focused Verifier run below.

## Existing dispositions and reviewed-head receipts

The exact sweeper P0 and three dependency P1 groups remain approval-only, with
severity preserved: Runner/Runner.Tests projects; all six project references,
including ArchitectureTests; and Hosting/YamlDotNet/xUnit declarations. The
approved plan and existing sweeper disposition remain their authority. Hari
reviews and merges protected changes. This round does not replace those findings
with a claim of unrestricted merge eligibility.

Hari's [namespace disposition](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/115#discussion_r4346757680)
says "Agreed, not valid" and "Dispute closed." The original is retained in the
review-comment response cited above. No namespace code repair is claimed.
The earlier admission projection repair and retry-summary clarification remain
the round 15 work described in [that record](review-15-dispositions.md).

The [head check-runs](raw/review-16-fixes/head-f79-check-runs.json) and
[workflow runs](raw/review-16-fixes/head-f79-workflow-runs.json) report completed
success for .NET run `39759595430` and docs run `39759595208`, both naming exact
head `f79c74cf23f5d0a1d30b399728f265c82208e676`. The retained
[security receipt](raw/review-16-fixes/head-f79-security-review.json) reports no
security issues for reviewed commit `f79c74cf23`. These are reviewed-head results,
not results for the pending round 16 source changes. The
[legacy status response](raw/review-16-fixes/head-f79-legacy-status.json)
contains no statuses (`total_count=0`); its aggregate `pending` value does not
identify a pending check in the successful check-run collection.

The [submitted reviews](raw/review-16-fixes/external-reviews.json) contain two
completed external code reviews: `5611667408` on `534ab8f70059f37bdd027781573023d971058b0b`
and `5612577376` on `f79c74cf23f5d0a1d30b399728f265c82208e676`.
Pending requests, security receipts and the author's comment submissions do not
count as additional completed external code reviews. Decision 6339066483 retains
the maximum of three, permits the third and final request after this bounded
repair/cold-review/push, and requires handoff after that completed review whatever
it reports. It waives the split for this PR; it does not authorize another round.

Actual image archive/smoke, Helm render/lint, real service-tag acquisition and
TFLint acceptance remain blocked as recorded in the existing README. This round adds no successful deployment, Azure operation or live-result claim.
Repaired-head workflow status, completed cold review and inline replies are
recorded in the PR after push.

## Local verification

The [Release build](raw/review-16-fixes/build-attempt-3.log) passes with zero
warnings and errors. Its [input inventory](raw/review-16-fixes/repository-inputs-before-build-3.json)
records the dirty source files used. The first two build attempts are preserved:
[attempt 1](raw/review-16-fixes/build-attempt-1.log) caught CancellationToken
parameter ordering and [attempt 2](raw/review-16-fixes/build-attempt-2.log) caught
a repeated constant test array; both analyzer findings were corrected without
suppression or weakened assertions. Each attempt has its own input inventory.

All [151 focused Runner cases](raw/review-16-fixes/runner-tests-attempt-1.xml)
and [382 existing Verifier host/comparison cases](raw/review-16-fixes/verifier-tests.xml)
pass with no failed or skipped cases. Runner coverage includes actual store/ledger
receipt failure then fresh-sweep recovery, immutable export replay with absent
local files, fail-closed evidence corruption, exact byte limits, and deterministic
admission timing around both the old cutoff and the declared deadline. Synthetic
fixtures exercise adapters and codecs; they establish no live Azure behavior.

The [docs scan](raw/review-16-fixes/check-docs.log) and
[authored whitespace check](raw/review-16-fixes/authored-diff-check.log)
are retained. The final scan reports 36 advisory warnings from unchanged captured upstream
Markdown described in the README. The [first scan](raw/review-16-fixes/check-docs-attempt-1.log)
also warned that the whitespace record had not yet been generated; that record
now exists, and the final scan clears the extra warning. Full suite and Kafka
component acceptance remain the existing exact-head PR workflow's responsibility.
