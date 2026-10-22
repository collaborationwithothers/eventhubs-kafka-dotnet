# A01 worker deployment and runner evidence

This directory records offline validation for the worker deployment and runner in
issue #114. The implementation follows the checked Revision 2 plan. Deployment
acceptance remains incomplete because the environment blocks required downloads;
the raw files here are synthetic fixtures and tool results, not live results.

The [continuation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470)
lifts the earlier cold-review churn stop without another split. The
[resumption record](resume-status.md) describes the normal-failure artifact and
workflow provenance fixes, along with the required override trip record.
Deployment acceptance remains incomplete. The required PR workflow passed at
the exact implementation head, as recorded below.
The [twelfth review](cold-review-12.md) also identified a required-plan
incompatibility: the mandated CLI inherits SDK write retries without a verified
disable control. The [source investigation](blob-retry-investigation.md) and
[narrow amendment proposal](retry-amendment-proposal.md) make that decision
reviewable. Hari adopted that narrow amendment in
[decision 6336034219](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6336034219).
The original P1 remains correct for the earlier contract; the amended contract
permits SDK retries inside the same deadlines while preserving conflict-stop.
The prior implementation passed required PR CI. External code review then
completed on `534ab8f70059f37bdd027781573023d971058b0b`; its confirmed admission
projection P1 is addressed by the [comment repairs](review-15-dispositions.md).
The security review completed with no findings at the earlier implementation
head. The direct `fixcomments` continuation in
[decision 6337640356](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6337640356)
supersedes the round-14 stop for these repairs and their required review.
The [thirteenth review](cold-review-13.md) covered every authored file and found
only four approval-only groups, with no non-approval P0/P1 or P2. The exact
sweeper P0 is now approval-only under
[decision 6336121859](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6336121859),
alongside the three plan-required dependency groups. Severity is retained and
Hari merges. That decision also bounds the remaining cold review through round
14 at that earlier checkpoint. The later user-directed continuation above
authorizes the current repairs and necessary review. The separate three-round
external code-review cap and merge gates remain unchanged.

- [Approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789)
- [Independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332608289)
- [Storage and cleanup decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257)

## Validation status

The repaired worktree builds with zero warnings/errors and passes 92 focused
Runner cases and 23 worker admission cases. The
[repair disposition](review-15-dispositions.md) links original reports,
pre-build input inventories and the retained first fixture failure. These local
checks do not substitute for repaired-head PR CI.

The persistent Terraform validation and native plan tests pass; their original
output is in [the Terraform logs](raw/terraform/persistent-test.log). The combined
[Release build](raw/retry-amendment/build-attempt-2.log) has zero warnings and errors;
the prior full local Runner report records 1001 passing tests in
[the ninth-review report](raw/review-9-fixes/runner-tests-attempt-2.xml).
The [focused LoadGen report](raw/review-4-fixes/loadgen-tests.xml) records 33
passing startup and compatibility tests. The
[rehearsal summary](raw/review-9-fixes/rehearsal-summary.json) and
[case traces](raw/review-9-fixes/rehearsal-cases.json) retain the synthetic phase and
failure-path checks. The existing pull-request workflow now supplies the required
full verification; the local runs remain separately identified.

[.NET verification run 39748172558](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/39748172558)
completed successfully for exact tested head
`d60de515d9404e0563cfe102c2b6a37968f1221f`, event `pull_request`, attempt 1.
The original [run metadata](raw/ci/run.json), [jobs](raw/ci/jobs.json) and
[artifact listing](raw/ci/artifacts.json) are retained. The listing contains zero
artifacts. The run-archive download was denied by the redirect host; the GitHub
connector successfully retrieved the complete decoded original
[job log](raw/ci/job-119721772459.log) through the documented REST job-logs route.
This is original job text, not a reconstructed ZIP or retained test XML.
[Producer records](ci-producers.json) distinguish both retrieval attempts.
The log confirms the exact checkout, locked restore, Release build, architecture
checks and warning-as-error negative control. Its checked reports record 8
Architecture, 306 Kafka, 308 LoadGen, 1003 Runner, 96 Scenarios and 2294 Verifier
passing tests. Enrichment is explicitly an empty scaffold. The separate mapping
coverage pass records 121 passing tests and 368 instrumented source lines
covered; branch completeness is checked by behavioral tests, with no measured
branch percentage claimed.

The [security review receipt](raw/ci/security-review.json) reports no security
issues for implementation head `d60de515d9`. Code review was still running at the
evidence checkpoint. These captures precede an evidence-only commit; the PR body
records any later final-head checks separately, without claiming that these
originals tested a later commit.

The [workflow preparation check](raw/review-5-fixes/workflow-preparation.json)
executes the actual private runtime preparation blocks with synthetic missing,
malformed and valid subscription inputs. Its nine cases check early failure,
private file permissions, suppressed identifier output and candidate-only
artifact paths. The exact supporting script is retained in
`review-5-producers.json`; it adds no CI job or live operation.

The [eleventh-review host report](raw/review-11-fixes/verifier-host-tests.xml)
records 382 passing Verifier startup and compatibility cases. Invalid generated
values paths fail options validation before handler, Blob or native calls;
omission and valid relative/absolute paths remain supported. The
[input inventory](raw/review-11-fixes/repository-inputs.json) hashes the updated
source before this build and targeted run. These local checks do not replace
the required PR workflow.

The deployment script retained locked restore when it found the runtime-specific
lockfile mismatch. The subsequent image attempt reached the official registry's
blob download and was denied. No container archive or image smoke result was
produced by that attempt. Helm rendering and the real committed service-tag
dataset also remain unverified.

The required download hosts are `centralus.data.mcr.microsoft.com`, `get.helm.sh`,
`download.microsoft.com`, and `tuf-repo-cdn.sigstore.dev`. The last host serves the
signature metadata required by the TFLint provider installer. No signature or
network-policy bypass was used. The failed commands remain under `raw/`. The
[authored acquisition status and provenance](acquisition-status.md) are retained
in reviewed scope outside that directory.

The authored diff passes the whitespace check. The unfiltered cached check
reports whitespace in original tool output and HTTP response captures; those
raw bytes are retained unchanged and hashed rather than reformatted.
The tenth-review documentation scan reports 36 advisory warnings in the captured
upstream Markdown: its relative includes/links are unavailable locally and one
source sentence uses non-ASCII punctuation. These original source bytes remain
unchanged; the pull request retains the warnings with this explanation.

## Review and provenance

`raw-manifest.json` records the producer, source revision, byte size and SHA-256
of every retained raw file. Evidence produced before the implementation commit
is identified as a dirty worktree based on the recorded source revision, with
input hashes where supplied. It does not claim a reviewed head commit.
The ninth-review checks retain a [repository input inventory](raw/review-9-fixes/repository-inputs-before-build-2.json)
captured before validation; it hashes repository inputs outside the evidence
directory and does not stand in for PR CI.
Earlier attempts are retained: the first build exposed a C# keyword collision in
test data accessors, and the first test run exposed an incorrect fixture count
for the fallback export attempt. Both test-fixture issues were corrected before
the passing build and test run; their original output remains in the manifest.
The eighth-review builds also exposed analyzer requirements in the projection
and new tests; those were corrected before the current passing build.
The first ninth-review test run found fixture setup errors: a watch listing
omitted its publication index, and normal verifier cases omitted the independent
broker input. The corrected fixtures retain every budget and watch assertion.
The earlier chart and deployment captures have no contemporaneous dirty-input
hashes. The post-execution source inventory cannot establish their historical
source bytes; the raw manifest retains their exact producer commands.

The [first cold-review report and dispositions](cold-review-1.md) are retained.
The second report records the findings and historical handoff before the
continuation decision. The [third report](cold-review-3.md) records the cost-scope
and dependency-construction findings. The [fourth report](cold-review-4.md)
records the account-projection and LoadGen options findings addressed in the
earlier checks. The [fifth report](cold-review-5.md) records the worker-observation,
redaction-startup and positive count-acceptance findings addressed in the current
local checks. The [sixth report](cold-review-6.md) found the acquisition-status
placement issue; the authored summary was moved unchanged into reviewed scope
while original tool captures remain raw.
The [seventh report](cold-review-7.md) found missing or invalid generation limits
and incomplete neighboring boundary tests. Shared generation/preflight validation
and the audited boundary fixtures address those findings. Large export fixtures
check metadata admission before a deliberate transport stop; recovery fixtures
stream hashes of synthetic sparse files. They do not claim live evidence or a
completed large private upload.
The [eighth report](cold-review-8.md) found incomplete public candidate fields
and the count-key mismatch. The candidate now projects verified provenance,
timings, gate decisions and eligible summaries. Its regressions cover durable
replay, tampered or missing originals, observed image receipts, count overflow,
redaction and preservation of failed execution status after cleanup.
The [ninth report](cold-review-9.md) found download totals checked after transfer.
Normal, failure and recovery tests now verify the unchanged individual and
aggregate byte ceilings before transfer, including prior verifier originals,
canonical reuse, raised configuration and zero-byte equality. Publication names
are checked as the existing exact member set before any Blob metadata request.
Subsequent cold-review results belong to the PR self-review. The required
workflow run ID and exact tested commit are recorded above.
The [tenth report](cold-review-10.md) found incompatible counter types in the
corroborating KQL union. The diagnostic counter now converts to `long` before
union. The [source audit](review-10-kql-audit.json) cites the original official
schemas, literal types, union behavior and conversion. Existing targeted tests
check query binding, private byte retention and CLI response parsing; these
checks do not execute KQL against Azure. All 35 targeted tests pass in
[the retained report](raw/review-10-fixes/query-tests.xml), using the unchanged
.NET assemblies from the ninth-review build. The updated query source is hashed
in [the tenth-review inventory](raw/review-10-fixes/repository-inputs.json).
The [eleventh report](cold-review-11.md) records the Verifier startup correction
and a disputed P0 concerning sweeper federation trust. The
[authority reconciliation](sweeper-authority.md) and
[disposition](resume-status.md#finding-dispositions) retain the finding's
severity and the exact already-authorized design. This was a disputed finding
at that checkpoint. The later decision 6336121859 supplies the current exact
approval-only disposition; the draft PR lists it for Hari without merge
clearance.
The twelfth review retained that dispute and found the CLI retry P1. Its P2
source-locator correction is applied in the query parser comment. Original
small upstream sources and the retry-source subset are retained under
`raw/review-12-blocker/`; the original large CLI archive remains in scratch,
with its immutable URL/hash in `review-12-source-receipts.json`. That archive
is not claimed as a committed raw file. The exact original fetch/filter
commands are authored provenance in `review-12-source-commands.json`.
The [store test report](raw/review-12-blocker/store-tests.xml) records 28 passing
existing CLI-boundary and ledger cases. They confirm application calls,
conflict-stop and uncertain-byte readback; fake processes do not exercise or
disable SDK retries and do not resolve the finding.
After the approved amendment, all 30 store and ledger cases pass in
[the retained report](raw/retry-amendment/store-tests.xml), including two new
synthetic committed-upload/CLI-412 cases against the rebuilt assemblies.
They use the real CLI adapter and ledger with a fake process, preserve the
conditional arguments/deadlines, and require conflict-stop with no returned
transition, readback or reinvocation. An explicit later read can observe the
committed bytes; this does not turn the failed transition into returned success.
They do not execute SDK HTTP retries. The new
[source inventory](raw/retry-amendment/repository-inputs-before-build-2.json)
hashes inputs before the passing build. Its first attempt caught analyzer
requirements for constant arrays and the assertion predicate overload; those
were corrected with the assertions unchanged, and the original output remains.
The new projects, references, protected-path edits
and provisioned `kubelogin` prerequisite require Hari's review. Persistent plans
and applies, workflow installation and live readiness remain with Hari under the
approved plan.
