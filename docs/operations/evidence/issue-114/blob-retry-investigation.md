# Read-only retry investigation for #114 / review 12

No repository edits, builds, tests, Azure operations or CLI invocations occurred.
Authority: /workspace/scratch/issue-114-authority-during-review-12/comments-114.json,
comment 6321098789, updated 2026-10-22T04:14:53Z. Its durable boundary paragraph
requires existing OIDC + explicit `az storage blob ... --auth-mode login`, no new
credential provider/package, 60-second CLI calls, 180-second operation, no write
retry, conflict stops, uncertain upload permits at most three bounded reads.

## Concrete incompatibility

`AzureCliRunStore.WriteAsync` currently launches one az process, but that does not
mean one write. Official Azure CLI commit da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e
requirements.py3.Linux.txt pins azure-cli-core 2.91.0 (line 7), azure-core 1.39.0
(line 11), azure-storage-blob 12.29.0b1 (line 91).

CLI storage commands.py lines 351-352 register upload with operations/blob.py
upload_blob. That function (643 onward, calls 695-707) forwards file data,
preconditions and upload options to BlobClient.upload_blob without retry override.
Storage _client_factory.py lines 122-162 obtains kwargs from core
prepare_client_kwargs_track2, constructs BlobServiceClient, then cf_blob_client
lines 175-196 derives the blob client. Core client_factory.py lines 132-183 adds
SSL verification, user-agent, headers, telemetry, custom hook, logging settings;
it adds no retry options and reads no retry configuration.

Storage SDK tag azure-storage-blob_12.29.0b1 resolves to commit
5eca695d87ce704eabe665ce42c698440521c289 (original ref response retained).
_shared/base_client.py:467 defaults to ExponentialRetry(**kwargs).
_shared/policies.py:608 defaults retry_total=3; lines 414-418 set counters;
440-465 only read kwargs/context options, no environment or CLI configuration.
500-551 decrement counters and rewind a replayable body, including a PUT;
554-590 catch AzureError and resend after backoff. _upload_helpers.py:100 onward
uses one PUT for a small block blob. The retry policy also supports that bytes
body, so passing an ordinary private local file does not prevent the retry.

Failure inference: service stores conditional PUT -> successful response lost ->
SDK retries original conditional PUT -> service returns 412 -> CLI returns error
-> AzureCliRunStore lines 67-70 labels Conflict -> RunLedger.cs:198 immediately
throws ledger_etag_conflict. Intended exact bytes may already have committed,
but the specified uncertain-write readback cannot run. There was no live failure
injection: the failure sequence is inferred directly from the pinned control flow.

## No supported per-process retry disable control found

Checked exact pinned CLI storage registration, upload argument registration
(_params.py:1127-1171), operation signature, storage client factory, core client
kwargs preparation, config entrypoint/prefix and SDK policy inputs. Also scanned
all non-test Python source under azure-cli-core/azure/cli/core and
azure-cli/azure/cli/command_modules/storage from the original immutable archive.
All retry-related source lines are retained in cli-retry-source-scan.txt.
There is no retry_total/no_retry/env retry propagation on this path. The generic
storage retry_wait/--retry-interval definition at _params.py:278 is not a retry
count and is not part of upload_blob's signature. Config's AZURE prefix cannot
supply an option that this client-kwargs path never reads.

Core SDK azure-core_1.39.0 resolves to
27bf66d7ecfbf25d8a607467e7cd3123c61e4cc5. Its policy similarly accepts Python
kwargs/context options rather than a retry environment variable, and it is not
the StorageRetryPolicy used for this upload. AZURE_CORE_RETRY_TOTAL=0 or
AZURE_STORAGE_RETRY_TOTAL=0 must not be represented as supported fixes.

ProcessRequest already has an Environment overlay; BoundedProcessRunner lines
20-21 applies it per process. That feature alone provides no retry control.
No supported minimal change meeting all the selected interface constraints was
identified. Do not claim an absolute impossibility across all unknown future
CLI versions; this is a concrete incompatibility at the official pinned source.
A decision must choose a supported CLI release/interface exposing an explicit
retry policy, or amend the mandated transport/interface to one that can specify
zero write retries. Both require authority evidence before implementation.
Do not silently substitute az rest/new credentials/packages, monkeypatch the CLI,
modify global CLI config, insert a proxy, or relabel all conflicts as uncertain.
The latter would change the explicit conflict-stop behavior.

## Same-class inventory

Runner's only Blob mutation process is AzureCliRunStore's upload; it serves
both conditional ledger updates and immutable evidence writes. EvidenceExporter
also reads back conflicts, unlike RunLedger's immediate conflict failure, but
both still inherit automatic HTTP write retries. The source of truth must be
fixed once for both consumers if authority chooses a supported replacement.

Other Runner mutations: TerraformEnvironment starts one plan/apply for creation
and one planned apply for destruction (lines 64-66 / 95-112), no application
retry loop. KubernetesRunCluster invokes kubectl apply and merge patches once
(403-408); AksKubeconfigBinding invokes namespace apply once (70). Its az aks
get-credentials retrieves access material and writes a local kubeconfig; it is
not an Azure environment creation. Helm is only version/lint/template here.
Do not conflate Terraform provider HTTP retry/LRO polling with rerunning a
Terraform apply, or assert all transport retries disabled without tracing the
actual pinned provider/kubectl source. This inventory proves application call
counts only; runtime kubectl is receipt-checked rather than pinned here.

LoadGen Program.cs:262-263 still constructs BlobClientOptions without retry
overrides for existing create-only publisher uploads (BlobRunArtifactPublisher
lines 103, 284). Verifier Program.cs:143-144 explicitly sets Retry.MaxRetries=0.
The LoadGen path is an adjacent SDK configuration concern and does not provide
a usable control for the mandated Azure CLI run store; its .NET default was not
independently traced during this finite investigation.

## Original sources and production

source-receipts.json maps fetched source files to immutable URL, byte count,
SHA-256 and a summary of the producer method. executed-producers.json is a later
authored transcript of the exact executed original fetch/filter/ref-query/byte-
comparison commands, copied from tool-call history. fetch-sources.py was authored
after the source receipts as a future minimal reproducible fetcher; it was never
executed and is not the original producer. Original CLI archive is azure-cli.tar.gz; its filtered source
matches live under tree/, and cli-retry-source-scan.txt records scan lines.
Both SDK tag-ref JSON responses are retained; sources first fetched by version
tag were fetched again by resolved commit and checked byte-identical.
