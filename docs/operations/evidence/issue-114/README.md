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
Release build has zero warnings and errors; all 271 Runner tests pass in
[the current local test report](raw/review-5-fixes/runner-tests.xml).
The [focused LoadGen report](raw/review-4-fixes/loadgen-tests.xml) records 33
passing startup and compatibility tests. The
[rehearsal summary](raw/review-5-fixes/rehearsal-summary.json) and
[case traces](raw/review-5-fixes/rehearsal-cases.json) retain the synthetic phase and
failure-path checks. Required
verification from the existing pull-request workflow is pending; no local run
substitutes for that evidence.

The [workflow preparation check](raw/review-5-fixes/workflow-preparation.json)
executes the actual private runtime preparation blocks with synthetic missing,
malformed and valid subscription inputs. Its nine cases check early failure,
private file permissions, suppressed identifier output and candidate-only
artifact paths. The exact supporting script is retained in
`review-5-producers.json`; it adds no CI job or live operation.

The deployment script retained locked restore when it found the runtime-specific
lockfile mismatch. The subsequent image attempt reached the official registry's
blob download and was denied. No container archive or image smoke result was
produced by that attempt. Helm rendering and the real committed service-tag
dataset also remain unverified.

The required download hosts are `centralus.data.mcr.microsoft.com`, `get.helm.sh`,
`download.microsoft.com`, and `tuf-repo-cdn.sigstore.dev`. The last host serves the
signature metadata required by the TFLint provider installer. No signature or
network-policy bypass was used. The acquisition receipts and failed commands
are retained under `raw/`.

The authored diff passes the whitespace check. The unfiltered cached check
reports whitespace in original tool output and HTTP response captures; those
raw bytes are retained unchanged and hashed rather than reformatted.

## Review and provenance

`raw-manifest.json` records the producer, source revision, byte size and SHA-256
of every retained raw file. Evidence produced before the implementation commit
is identified as a dirty worktree based on the recorded source revision, with
input hashes where supplied. It does not claim a reviewed head commit.
The current checks retain a [repository input inventory](raw/review-5-fixes/repository-inputs-pre-validation.json)
captured before validation; it hashes repository inputs outside the evidence
directory and does not stand in for PR CI.
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
local checks.
Subsequent cold-review results belong to the PR self-review. The required
workflow run ID and tested commit are pending PR CI.
The new projects, references, protected-path edits
and provisioned `kubelogin` prerequisite require Hari's review. Persistent plans
and applies, workflow installation and live readiness remain with Hari under the
approved plan.
