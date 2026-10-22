# A01 worker deployment and runner evidence

This directory records offline validation for the worker deployment and runner in
issue #114. The implementation follows the checked Revision 2 plan. Deployment
acceptance remains incomplete because the environment blocks required downloads;
the raw files here are synthetic fixtures and tool results, not live results.

The [continuation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470)
lifts the earlier cold-review churn stop without another split. The
[resumption record](resume-status.md) describes the normal-failure artifact and
workflow provenance fixes, along with the required override trip record.
Deployment acceptance and the required PR workflow evidence remain incomplete.

- [Approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789)
- [Independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332608289)
- [Storage and cleanup decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257)

## Validation status

The persistent Terraform validation and native plan tests pass; their original
output is in [the Terraform logs](raw/terraform/persistent-test.log). The combined
[Release build](raw/review-11-fixes/build.log) has zero warnings and errors;
all 1001 Runner tests pass in
[the current local test report](raw/review-9-fixes/runner-tests-attempt-2.xml).
The [focused LoadGen report](raw/review-4-fixes/loadgen-tests.xml) records 33
passing startup and compatibility tests. The
[rehearsal summary](raw/review-9-fixes/rehearsal-summary.json) and
[case traces](raw/review-9-fixes/rehearsal-cases.json) retain the synthetic phase and
failure-path checks. Required
verification from the existing pull-request workflow is pending; no local run
substitutes for that evidence.

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
workflow run ID and tested commit are pending PR CI.
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
severity and the exact already-authorized design. This is not a clean-review
claim or merge clearance; the draft PR must disclose the dispute for Hari.
The new projects, references, protected-path edits
and provisioned `kubelogin` prerequisite require Hari's review. Persistent plans
and applies, workflow installation and live readiness remain with Hari under the
approved plan.
