# Archive readiness smoke

The archive smoke prepares live evidence for the proposed persistent evidence archive. It transfers a small synthetic Blob fixture between the real ephemeral and persistent storage accounts, checks create-only conflict behavior and verifies the destination bytes. It does not run Kafka workers or establish scientific results.

The [preparation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6894417226) authorizes code, offline tests and a reviewable prerequisite PR. It grants no deployment, spending, persistent apply or workflow dispatch. The [product specification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/08dc368daa2f1925cd9f478378e03133b0a503d4/items/494/plan-observability-evidence.md), amended by that decision's receipt correction, remains subject to its live evidence and approval gates.

## Before deployment

Hari reviews and merges the protected changes, approves a dated cost estimate and plans/applies `infra/persistent`. Only Hari executes that root. Replace the private `PARCHWELL_PERSISTENT_OUTPUTS` environment secret with the new Terraform output JSON, keeping its IDs and exporter configuration out of public artifacts.

The new `evidence_archive` output has exactly `storage_account_id`, `account_name`, `blob_service_uri`, `container_name`, `container_id` and `retention_days`. A separate sensitive `workflow_principal_id` output exposes the existing workflow principal to the ephemeral root through its private remote-state reference. It is not an agent identity or a new credential. The workflow identity also needs its existing backend, resource-management and role-assignment permissions; a mock plan cannot establish effective access.

The runner operator must supply Azure CLI 2.92.0 with its own Python environment containing `azure-storage-blob` 12.29.0b1. The probe verifies both versions and the interpreter path, and fails closed on a mismatch rather than installing or upgrading them. The pins follow the [CLI release](https://github.com/Azure/azure-cli/releases/tag/azure-cli-2.92.0), its [Linux dependency](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli/requirements.py3.Linux.txt#L91) and [Python-location formatter](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli-core/azure/cli/core/util.py#L442).

Persistent archive capacity, its private endpoint and transactions are paid resources. The smoke's existing ephemeral Terraform root also creates a Premium Event Hubs namespace and other normal ephemeral resources, although no Kafka workload runs. Price those resources and expected execution/retention time before authorizing a dispatch. The dated retail illustration in the product specification is an estimate, not a spending approval or measured invoice.

## Authorized execution

After those prerequisites, Hari or a separately authorized successor may dispatch [Archive smoke](../../.github/workflows/archive-smoke.yml) on `main`. It has no other trigger or user-controlled workload selection. Use one controlled dispatch, one phase and one positive/negative fixture pair; retain failure evidence rather than automatically rerunning it.

The workflow shares the existing ephemeral concurrency group and environment-bound OIDC identity. It prepares private inputs, enables the serialized renewable Azure CLI wrapper and runs version/configuration preflight before ephemeral provisioning. It then uses the existing lifecycle init, plan and apply commands, followed immediately by the existing private DNS check and storage-readiness wait. It does not apply persistent Terraform or start AKS.

Only this workflow enables `TF_VAR_archive_smoke_enabled=true`. Its temporary `archive-smoke-source` container gives the existing workflow principal Blob Data Contributor on that container, allowing a synthetic fixture to be seeded without an account-wide grant. The archive grant is limited to `run-evidence`. Ordinary scenarios retain the default `false`; they do not create the source container or consume its new principal output. A default-config sweeper destroy still removes the smoke resources recorded in ephemeral state.

The probe commands are fixed:

```sh
python3 infra/scripts/archive/smoke.py preflight
python3 infra/scripts/archive/smoke.py run
```

These are live workflow operations, not agent-session verification commands. They read the private prepared `phase.json`, `persistent-output.json` and, for `run`, the applied `ephemeral-output.json`. Account endpoints and container identities come from those validated files. No connection string, access key, SAS URL or agent service principal is accepted.

The fixture is synthetic and bounded to 8192 bytes by the preparation scope. The probe verifies its source ETag, length and SHA256; conditionally creates the destination; independently reads it back; deliberately attempts another create; checks the expected conflict; and verifies unchanged content. A failed negative check is not success merely because a CLI command failed. Fresh CLI processes exercise new connections; evidence must identify actual execution conditions rather than treating an offline fixture as a deployed observation.

## Controls and public evidence

The product archive manifest, completion-marker and public-receipt contracts are pinned in the linked specification and adopted correction. The canonical manifest has `completion:null`; its readiness describes data verification. A separately verified completion marker and matching true receipt are also required before the probe reports complete retained evidence.

A false receipt has `completion:null` and an approved gap. It can reference independently verified canonical bytes whether their `archive_complete` is true or false. Failure after canonical verification never rewrites those immutable bytes or permits publication. The smoke identifies `run_kind=smoke`, `purpose=archive-readiness` and `scientific_admissible=false`.

Private spooling remains outside artifact staging. GitHub receives only these fixed small files, with the existing 90-day artifact policy:

- `archive/archive-smoke.json`: checks, observed byte count, duration and gaps.
- `archive/archive-manifest.json`: canonical bytes when available.
- `archive/archive-receipt.json`: verified control references or an explicit incomplete state.

The original synthetic destination fixture and remote controls remain under the run-scoped archive prefix for lifecycle processing. Source fixture cleanup is part of destroying the complete ephemeral layer. No production event body, claim-check body, backend configuration or CLI error dump is uploaded.

An archive command has a 600-second monotonic deadline, including SDK requests/backoff and hashing. A subprocess also has the existing 60-second invocation bound and a child file-size limit of 256 MiB. There is one helper pass, without an outer transfer retry. The intentional negative probe is a distinct bounded check. Native retries are still enabled in the [pinned SDK policy](https://github.com/Azure/azure-sdk-for-python/blob/azure-storage-blob_12.29.0b1/sdk/storage/azure-storage-blob/azure/storage/blob/_shared/policies.py#L593-L635); one CLI call is not one HTTP request.

The persistent Hot/LRS account disables public access, shared keys, versioning and Blob/container soft delete. Its `run-evidence` creation-age deletion policy is 90 days, as supported by the [pinned AVM inputs](https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/blob/5695e5ec21744a3765051677d9e82bfba16b5d98/variables.management_policy.tf). Azure processes lifecycle actions periodically; this is not a promise of deletion at the exact 90-day instant ([official lifecycle behavior](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview)).

## Failure and continuation

The probe returns 0 for verified completion, 1 for an incomplete transfer/evidence check and 2 for unsupported or invalid configuration. Available failure evidence remains explicit. Ephemeral destroy and the independent dispatch-end AKS stop still run after failures where the runner survives. The sweeper recognizes this workflow's active runs and completion time; runner loss is not proof that cleanup executed.

Before claiming readiness, inspect source/destination access, private DNS/routing, deployed CLI/SDK/interpreter versions, exact observed bytes and timings, the classified conflict, independent hashes and both cleanup outcomes. Record whether connections were fresh or already open and whether the system was idle or under load. A tiny synthetic transfer does not establish feasibility at the product archive's maximum volume, metadata capture, Kafka telemetry reception or backend metric limits.

If a prerequisite is missing or the one dispatch fails, preserve diagnostics and hand off the specific finding. No automatic replacement dispatch, broader role, public endpoint, timeout increase or scientific rerun is authorized. The future archive, telemetry and inline duplicate-reporting PRs remain separate work. Agent Azure identities stay in [the separate identity issue](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/496).
