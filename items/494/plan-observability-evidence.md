# Preserve diagnostic evidence and export native Kafka statistics

Future runs retain the evidence needed to investigate retries, errors and duplicate messages after ephemeral storage is destroyed. Native statistics and application-retry metrics remain available in Application Insights; complete reports and record metadata are archived in persistent Azure Blob Storage.

## Start here

- Issue: [#494](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494).
- Revision: R1, 2026-11-15. Status: specification for independent plan check and Hari's approval; not permission to implement, provision or dispatch.
- Base: `22bcce4b53f4031ec467c2fd6d2b2ff314291fb0` on `main`.
- Implementation model: **GPT-6.1 Terra, high reasoning effort**. Select that model and effort in the client; prose cannot change the running model.
- Plan name: `observability-evidence`. One ordered set: PR1 archive; PR2 telemetry; PR3 inline duplicate observations. Each merges green independently. Each implementing session selects the earliest unmerged entry under factory.md.
- Read the common sections and the selected PR section. The native field catalogue belongs to PR2. Do not implement all entries in one pull request.
- Governing process: [factory.md Item lifecycle](../../docs/agents/factory.md#item-lifecycle), [pull-request rules](../../docs/agents/pull-requests.md), and [AGENTS.md](../../AGENTS.md). This specification does not relax their gates.

## Goal and authority

Authority is the issue body, [the table-plan decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6892428668), [the agent-access scope separation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6893629386), and [the decisions reached by grilling](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6893649088), in creation order. The last decision supersedes the body's artifact-only destination and causal labels for duplicate categories. The existing tool assignment is [Codex](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6892754688). A preexisting `ready-for-agent` label is not approval of this new plan commit.

The parent [producer-batch proposal](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347) supplies context; no unadopted parent recommendation silently changes this issue's contract. The published B10 result is not changed, reinterpreted or rerun by this work.

Decisions fixed in this specification:

1. Numeric native fields, finite states and application resends use the existing OpenTelemetry meter/exporter pipeline. Metric aggregation is not lossless retention of native reports.
2. Raw reports, producer bookkeeping, per-repeat/phase verifier evidence and consumed-record metadata survive for at least 90 days in a dedicated persistent Blob archive. Lifecycle deletion is age-based and asynchronous, not a promise of deletion at the exact 90-day instant.
3. Consumed evidence includes raw keys and ordered headers, coordinates, decoded identity, sequence and payload hash/length. Message bodies and claim-check payloads are outside the archive.
4. Log Analytics retention stays 30 days. Metrics must remain usable by later dashboards and alerts. This work does not deploy dashboard or alert resources.
5. Capture and archive completeness are separate from Kafka scientific classification. Missing required capture, exhausted archive bounds or failed transfer prevents a future evidence pack from claiming complete retained evidence. Existing scientific validity, outcome classes, timeout/restart independence and unconditional cleanup remain authoritative.
6. The duplicate decomposition is inline in the existing result formats; there is no duplicate sidecar. Its categories describe identity observations, not a proven cause.
7. [Agent Azure identities (#496)](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/496) are separate. This plan contains no agent service-principal provisioning, cloud-session credential setup, custom credential broker, gateway or MCP server. The existing workflow's archive transfer permissions are in PR1.

## Ordered scope and boundaries

| Entry | Deliverable | Dependency | Estimate dated 2026-11-15 |
| --- | --- | --- | --- |
| PR1 `494-evidence-archive` | Persistent archive, metadata capture, transfer completeness and small public manifest | Approved smoke prerequisite and Hari's persistent apply as below | Estimate: 25-45 files, 2500-4500 changed lines, based on infrastructure, capture, phase and tests |
| PR2 `494-native-telemetry` | Fixed native field registry, every existing producer/consumer callback path, application resend telemetry | PR1 merged and its capture/transfer contract available | Estimate: 35-60 files, 3000-5500 changed lines, based on the registry and existing client factories |
| PR3 `494-duplicate-breakdown` | Shared identity arithmetic and inline result propagation, compatibility, tests and mutation coverage | PR1 and PR2 merged | Estimate: 40-55 files, 1800-3000 changed lines, based on traced codecs and judgement paths |

These are planning estimates, not measurements or size caps. Do not trim necessary work to meet them. Private file layout and method bodies remain implementer choices within each section's allowed paths. A new contract, requirement or scope needs an amendment, not an implementation guess.

Allowed shared documentation paths: `docs/blueprint.md` only at the archive, observation and telemetry consequences in ADR-007/012/019/020/021 that this plan changes; `docs/engineering/terraform.md` only to remove the obsolete claim that every persistent evidence store is removed; relevant existing `docs/operations/` contracts and workflow mirrors; `items/494/state.md` during implementation. Each changed rule is stated once in its owning document and linked elsewhere. Do not create `docs/adr/`.

Forbidden changes: `results/**`, `scenarios/**/expected.yaml`, scenario workloads/settings/repeat counts, historical reproduction scripts or archived evidence, agent-authentication policy/credentials, workflow triggers, package/project/assembly-reference additions, unrelated infrastructure and the external Terraform backend. Any required workflow source/mirror changes remain byte-identical. Protected merge authority stays with Hari.

## Common numbered requirements

C1. WHEN any selected PR is implemented, THEN its changed behavior and the tests proving it land together, and every affected consumer of its external format is updated in that PR. No unmerged future PR supplies a test dependency needed to merge the current one.

C2. WHEN a future run contributes to a verdict, THEN its scientific result and its archive-completion evidence are checked independently. Archive failure never becomes Kafka loss, a reported delivery failure, an invented zero, or a rewrite of the current candidate's scientific status.

C3. WHEN collection fails, is cancelled, or exhausts a bound, THEN retain available diagnostics and an explicit gap, report unsuccessful evidence completion, and run existing Job/ephemeral cleanup. A hard runner termination can prevent final logging; do not claim `always()` proves execution after machine loss.

C4. WHEN an existing fixture is rejected by a new approved check, THEN either repair the fixture while keeping every assertion and the boundary it exercises, or retain deliberately invalid input and change its expected result to the exact approved check output. List each changed fixture and its forcing check in PR notes. This permits no weakened, deleted or skipped assertion.

C5. WHEN a metric or archive fact is described publicly, THEN cite its pinned source or measured evidence, mark any inference, and label estimates with date and basis. Use ASCII punctuation in repository prose.

C6. WHEN a required deployment assumption has not been observed, THEN leave its acceptance gate open and identify the responsible action and evidence. Mock tests and a linked issue do not establish live readiness.

## Resource and work bounds

Policy values below require Hari's plan approval. Values accepted during grilling are the archive, chunk and row limits; other finite execution values are proposed implementation bounds for that approval.

| Work | Bound | Exhaustion action |
| --- | --- | --- |
| New logical archived object bytes per phase | 17179869184 bytes (16 GiB), summed without retry duplication | Record gap, stop accepting excess data, continue independent cleanup; block complete publication |
| Consumed-metadata chunk, including LF | 134217728 bytes (128 MiB) | Seal and rotate before the next whole row |
| Consumed-metadata row, including LF | 1048576 bytes (1 MiB) | Record first missing ordinal and sticky capture gap; never truncate a row |
| One transferred source/readback object | 268435456 bytes (256 MiB) | Reject before transfer, or stop bounded child write; record gap |
| Inventory entries, including control objects | 1024 | Record gap; no completion marker |
| Canonical archive manifest | 1048576 bytes | Record inventory gap; no success marker |
| Archive completion and public receipt each | 65536 bytes | Record gap; no success claim |
| Archive command | 600 s aggregate monotonic deadline, including enumeration/retries/hash/readback | Record gap and continue cleanup |
| End-to-end object transfer | At most 3 attempts inside that deadline; no nested retry multiplication | Record gap and continue remaining independent objects if time remains |
| Transfer subprocess | Existing 60 s wrapper limit, additionally bounded by remaining archive deadline | Kill/wait through existing process-group cleanup and record gap |
| Private transfer spool | At most source plus readback objects concurrently, 536870912 bytes plus bounded controls | Free-space check before work; record disk-space gap |
| Offline telemetry benchmark | One baseline and one expanded pass over the same fixed 256-report replay; no repetitions for a preferred result | Record measurements and limitations; a failure blocks the relevant acceptance |
| Archive live smoke | One controlled dispatch after approved preparation, at most 2 phases and one small positive/negative transfer fixture per phase; no scientific retry | Preserve failures and hand off; no automatic second dispatch |
| Premerge mutation preparation per affected PR | One full mutation-only profile with the existing 600 s limit; geometry/timing work only for the final changed target set, at most 2 bounded sizing passes, at most 10 surfaced finding groups | Save work to the rule-12 backup branch and hand off before merge if qualification remains unavailable/stale/over-budget |
| Applicable hosted mutation acceptance | One full configured main-only dispatch after each affected merged target revision; existing 16200 s shard/300-minute job/no-retry safeguards | Record incomplete/failing evidence, keep publication blocked, hand off under existing rules |
| Verification | One focused relevant pass, mandatory gates in repository order; repeat for changed behavior/failure/new evidence only | Record failure; resolve within the selected contract or hand off |
| Cold review | The cumulative budget of pull-requests.md rule 8 | Use its exact exhaustion action; no new-session reset |

Every capture/transfer validator tests below, at and above its boundary, empty input, invalid types and precedence with otherwise valid fields. Large-limit tests use bounded synthetic streams or metadata, not multi-GiB allocations. A benchmark output or population count is not mutation acceptance.

## Cost, table plans and cardinality

Estimate inputs were read on 2026-11-15 from official Azure Retail Prices APIs, GBP. These are retail rates, not measured invoice amounts or a claim about discounts/free allowances.

| Data set | Destination and choice | Alternative plans and consequence |
| --- | --- | --- |
| Native metrics and application-retry counter | Application Insights `AppMetrics`, Analytics | AppMetrics supports neither Basic nor Auxiliary. Preserve log dimensions for KQL dashboards and alerts; do not enable dimensioned metrics-store billing implicitly |
| Existing application logs | Existing `AppTraces` configuration; no table migration | Basic is supported, Auxiliary is not. This plan does not change the shared table's existing query/alert behavior |
| Raw statistics, producer/verifier records and metadata | Persistent Blob; no LAW table | No Analytics/Basic/Auxiliary ingestion for these bytes; immediate authenticated Blob reads |
| Archive locator/receipt and existing candidate/log/pod evidence | GitHub Actions | Keep bulk archive objects out of public artifact staging. Public-repository artifact billing remains unverified; do not assert a charge or unconditional exemption |

Current official documentation supports full KQL across Basic/Auxiliary tables; older single-table/no-join descriptions are stale. Auxiliary has no alerts and slower dashboard queries; Basic has Simple Log Alerts and scan-based query charges. Neither changes AppMetrics eligibility. No new custom LAW table or Logs Ingestion API path is needed after the archive decision.

Verified retail inputs: Analytics chargeable ingestion GBP 2.1653/GB; Basic 0.4699/GB; Auxiliary 0.0451/GB; applicable scan queries 0.0047/GB. Hot LRS first-tier capacity is GBP 0.0144/GB-month. A standard persistent private endpoint is GBP 0.0075/hour plus first-tier ingress/egress processing GBP 0.0075/GB. Price the Standard meter, not Fixed Private Endpoint T1 or Standard Service Endpoint.

Estimate dated 2026-11-15: keeping one endpoint for 90 days is GBP 16.20; an illustrative 16 priced-GB held for three billing months is GBP 0.6912 Hot capacity. Their subtotal is GBP 16.8912 before transactions, extra retention, DNS, peering/bandwidth, endpoint processing, export ingestion or existing service costs. This is a synthetic capacity scenario, not measured archive size, a per-run charge or a spending approval. The endpoint continues to cost money between runs and after one run's 90-day retention.

Hot is the proposed initial tier because diagnosis can read immediately without retrieval-tier fees and it follows the existing storage pattern. Cool/Cold capacity alternatives are GBP 0.0079/0.0034 per GB-month, with higher read/retrieval charges and minimum tier residence of 30/90 days. Archive tier has a 180-day minimum and delayed rehydration, so it does not fit an assumed immediately readable 90-day archive. Do not claim Hot or Blob is universally cheapest.

Before a paid live run, the implementing PR supplies a dated estimate with explicit handles, brokers, topics/partitions, report/export intervals, client lifetimes, serialized export bytes, compressed archived bytes, transactions, readback and anticipated reads. Compute `estimated_metric_ingestion_GBP = 2.1653 * estimated_billable_Analytics_GB`; identify billing-envelope uncertainty and shared allowances. Raw JSON report size is not the size of AppMetrics ingestion. A serialized exporter replay is preparation; actual Azure billing/ingestion remains live evidence.

The PR2 registry pins instrument count. Per-run series/point count is a union over client lifetimes and attribute tuples, not that fixed count alone. The baseline native statistics interval is 15000 ms. Preserve each selected client's resolved existing interval, including scenario-specific intervals, and existing exporter settings. SDK stream/cardinality limits are per provider/process; backend active-series limits span a longer window. Test the production registry with all subscribed meters and assert no silently ignored instrument or `otel.metric.overflow` result. Never reduce field coverage silently to fit a quota or budget. Record an unresolved admission limit and obtain a decision before the costly run.

Measured starting evidence: one existing C1 public artifact from [run 43689822806](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43689822806), SHA-256 `e804d6a5f600bb15a535afaf2afba5fe061575a8e8aa8211bad4f48ea583b582`, is 470150 compressed bytes and 5434783 uncompressed bytes in 240 files. This is partial existing retention, not a full future archive. It excludes missing ordinary publications and the proposed consumed metadata stream. A serialization-based estimate using a synthetic 927-byte metadata row and the current timeout schedule gives 574740000 bytes for 620000 repeat-plus-final observations before additional copies. It is not a measured bound; hence explicit gap handling at the approved archive limit.

## PR1: evidence archive

### Numbered PR1 requirements and test map

A1. WHEN persistent configuration is prepared, THEN create the exact private archive resource/output/role/lifecycle contract below. Terraform tests assert all configured values; only live smoke proves deployed access.

A2. WHEN a verifier consumes an in-window raw record, THEN write the metadata contract before identity/ownership projection loses bytes. The raw-key/header/hash test below pins null, empty, malformed and binary cases.

A3. WHEN a row/chunk/aggregate/capture boundary is reached, THEN apply the exact byte/count and gap policy below. The boundary tests exercise limit-1, limit and limit+1 with valid unrelated fields.

A4. WHEN a prepared phase has success, failure or incomplete workers, THEN derive the expected inventory from identities/publications, include missing expected entries explicitly, and transfer only the allowed data classes. The failed-run inventory test pins not_started versus missing.

A5. WHEN a source or destination is transferred, THEN enforce conditional identity, bounded child/file writes and independent length/SHA256 verification before counting it. The ETag/readback/conflict tests below pin each failure.

A6. WHEN every expected object and capture is complete, THEN write and verify the canonical manifest and completion marker last. Otherwise preserve an incomplete manifest and no accepted completion. The marker-order test injects failure at every stage.

A7. WHEN archive execution finishes or fails, THEN upload only the small public manifest/receipt, keep its readiness separate from candidate science, and run existing cleanup. The workflow and secret-sentinel tests cover every existing workload job path.

A8. WHEN a future evidence pack is prepared, THEN require its contributing phases' exact completion/manifest bindings and expose missing coverage; do not reinterpret an existing result or published verdict.

### 1. Existing seams and changes
- infra/persistent/locals.tf:2-28 provides shared workload list, generated names, shared tags, blob private DNS name. Add names.archive_storage = "stparchwellarc${var.naming_suffix}" and names.archive_blob_private_endpoint = "pep-archive-blob-${var.naming_suffix}". Reuse private endpoint subnet/DNS outputs, never add public access. No new identity needed.
- infra/persistent/variables.tf:72 already accepts workflow_principal_id, currently described for registry push; broaden description to registry push and archive writer. Do not introduce separate archive identity/credentials. Persistent identity.tf creates workload identities with no grants; observability.tf configures unrelated persistent workspace/Insights and should not be changed for archive. providers.tf already has storage_use_azuread=true; versions.tf pins Terraform 1.16.6, AzureRM4.81.0/AzAPI2.13.0. No provider upgrade needed.
- Add persistent archive storage resource file using existing AVM storage module, not a custom module. Current ephemeral pattern is infra/ephemeral/storage.tf:2-58: AVM0.10.0, StorageV2/Standard_LRS/Hot, public access false, keys false, OAuth default, HTTPS/TLS1_2, nonpublic containers, blob PE in shared subnet/DNS. Keep those controls for archive. Ephemeral storage itself need not move or be retained.
- Persistent output contract `evidence_archive` = { storage_account_id, account_name, blob_service_uri, container_name, container_id, retention_days }. Container name `run-evidence`; retention_days=90. It contains no key/token or signed URL. Output resource IDs still stay in private persistent-output JSON; public manifest identifies destination by DNS hostname/container/path only. No tenant/subscription/principal IDs in public receipt.
- Persistent container RBAC: existing var.workflow_principal_id gets Storage Blob Data Contributor on archive container; existing workflow's source grants must cover read/list on the exact ephemeral evidence containers. No workload identity gets archive access, no account-wide contributor or new agent SP. Actual source grants are deployment prerequisite to verify; pattern workload grants are in infra/ephemeral/identity.tf:2-35 and do not establish workflow access. Agent SP #496 is separate.
- PhasePaths.cs currently owns fixed start-line/producer/result/candidate/sweep paths. Add verifier evidence paths there, mirrored exactly in phase.py contract: `<run>/<phase>/r<repeat>/consumed-metadata/<capture-id>/chunk-000001.jsonl`, `capture.json`; final sweep uses `<run>/<phase>/consumed-metadata/<capture-id>/...`. capture-id must bind mode/job identity and pass bounded ASCII-segment validation. Never key solely by repeat where normal/final-sweep windows differ.
- WindowConsumer.cs is the common raw-record seam before MessageIdentityReader projection. MessageIdentityReader.cs decodes only chosen identity headers and selectively hashes payload; ConsumedRecord.cs currently cannot recover raw keys/all ordered headers/timestamp. Add capture observer at this boundary, retaining existing scientific projection unchanged. All WindowConsumer call sites must get it: VerifyRunHandler.cs, B10AbruptVerifyHandler.cs, B10FinalSweepHandler.cs. It must finalize in handler finally paths, even if input-budget accounting/decoding/drain fails. Archive capture faults are sticky separate evidence gaps, never converted into scientific failure assessments. Raw record seen before scientific accounting may itself trigger the existing scientific bound; preserve the attempted capture and gap.
- phase.py:439-521 prepares private/candidate/artifact directories and freezes run/source. Extend validated persistent-output consumption/frozen phase metadata here. Existing download_blob:1438 uses show + ETag If-Match and then read_bytes; it is unsuitable for accumulating a 16GiB archive. Existing candidate_download:1626 has three tries and preserves existing candidate semantics. Archive is a separate command and state member, not a rewrite of candidate.status/gate/full_allowed or B10 classification.
- b10_abrupt.py:15-20 already fixes FIRST_FILES/SUCCESSOR_FILES/REPORT_FILES; collect:981 and collect_job:1157 retain diagnostic run files. Reuse these inventory rules and run bindings, include failed/no-kill and incomplete observed files, and do not relabel diagnostic evidence scientific. Do not relax any scientific checks to collect it. Avoid sweeping arbitrary paths into the archive.
- scenario.yml has four Azure workload jobs with candidate/pod upload then always delete/destroy (208-253,403-447,632-683,836-875). Add archive and upload_archive_manifest between evidence collection and delete/destroy in each applicable workload path, with `if: always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'`; missing ephemeral output records a gap rather than skipping cleanup. Require these outcomes in report.WORKLOAD_STEPS. Do not change triggers/concurrency. docs/operations/workflows/scenario.yml.txt currently compares byte-for-byte equal and must change identically. Current upload_pods uploads entire artifacts directory: raw staging there would leak bulk evidence to GitHub. Spool all raw data under private/archive; only small manifest/receipt enter artifacts/archive.

### 2. Verified exact lifecycle module contract
The official AVM v0.10.0 tag resolves to source commit 5695e5ec21744a3765051677d9e82bfba16b5d98. Exact module inputs below were checked against that pinned source.
- https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/blob/5695e5ec21744a3765051677d9e82bfba16b5d98/variables.management_policy.tf defines `storage_management_policy_rule` (map), not an invented lifecycle_policy argument.
- https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/blob/5695e5ec21744a3765051677d9e82bfba16b5d98/main.management_policy.tf passes that map to its built-in modules/management_policy.
Use literal input:
```hcl
 storage_management_policy_rule = {
   delete_run_evidence = {
     name = "delete-run-evidence-after-90-days"
     enabled = true
     filters = { blob_types = ["blockBlob"], prefix_match = ["run-evidence/"] }
     actions = { base_blob = { delete_after_days_since_creation_greater_than = 90 } }
   }
 }
```
Explicit blob_properties = { versioning_enabled=false, delete_retention_policy={enabled=false}, container_delete_retention_policy={enabled=false} }; no tier transitions, snapshot, restore or change-feed additions. Schema verified at same commit's variables.blob_service.tf. Built-in AVM fulfills need: no AzureRM fallback or new provider is justified. AzureRM4.81.0 official docs also expose the same base_blob creation-age field, read at https://github.com/hashicorp/terraform-provider-azurerm/blob/v4.81.0/website/docs/r/storage_management_policy.html.markdown, but unnecessary to instantiate separately.
Official Azure lifecycle docs read 2026-11-15 via curl:
https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-policy-structure (run conditions: daysAfterCreationGreaterThan applies to current blob; action eligible after number of days exceeds value).
https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview (policy processes objects periodically; initial policy changes may take up to24h).
Say "90-day creation-age deletion policy; actual deletion follows periodic lifecycle processing", never exact expiry-at-90-days or a hard maximum storage duration. No claim that this has been deployed or observed.

### 3. External configuration and command contract
- New zero-argument command: `bash infra/scripts/phase/phase.sh archive`, dispatched as `archive`: (0,"archive") by phase.py.main. Extra args rejected before side effects.
- Archive is mandatory for a prepared workload, including failed/incomplete/scientifically invalid runs. No operator switch that makes a missing capture successful. Existing diagnostic-only selection retains its meaning.
- Input remains S_PERSISTENT_OUTPUTS containing Terraform output `evidence_archive`; validation happens before making directories/Azure calls. Unwrap output as existing code does; enforce exact member types, HTTPS Azure Blob host, safe account/container names and mutual consistency; reject query/userinfo/fragments/custom endpoints/path traversal. Read ephemeral source from private lifecycle output, never a caller-supplied arbitrary URL.
- Phase preparation exports only `PARCHWELL_ARCHIVE_DIRECTORY=<private>/archive` and `PARCHWELL_ARCHIVE_MANIFEST_DIRECTORY=<artifacts>/archive`. Keep destination endpoint/container inside frozen private phase.json rather than proliferating environment controls. Export `PARCHWELL_ARCHIVE_MANIFEST_PATH=<artifacts>/archive/archive-manifest.json` for the fixed upload path. No new Azure credential envs; use existing workflow OIDC/serialized Azure CLI session.
- Freeze nonconfigurable policy constants as bytes/seconds in phase archive module: ArchiveMaxBytes=17179869184, ArchiveMaxObjectBytes=268435456, ArchiveMaxObjects=1024, ArchiveDeadlineSeconds=600, ArchiveAttempts=3, MetadataChunkMaxBytes=134217728, MetadataRowMaxBytes=1048576. These are fixed policy, not measured feasibility. All manifests/marker plus all copied bytes count toward per-phase16GiB; reserve their bounded size before selecting data. Data objects <=1022 so manifest and completion marker fit max1024 objects. Metadata chunks have stricter128MiB cap; raw preexisting objects retain256MiB maximum. Use maximum JSON depth 32, ASCII identifier/relative-path length 1024 characters, UTF-8 diagnostic text 2048 bytes, and at most 100 diagnostics. The manifest remains complete-or-incomplete; overflowing diagnostics sets a gap rather than hiding failure. Reuse stricter existing identity validation where applicable.
- Verifier options contract (own section `Verifier:ConsumedMetadata`): Enabled=true, MaxRowBytes=1048576, MaxChunkBytes=134217728, MaxBytes=17179869184. Bind these exact option names as `Verifier__ConsumedMetadata__Enabled`, `Verifier__ConsumedMetadata__MaxRowBytes`, `Verifier__ConsumedMetadata__MaxChunkBytes`, and `Verifier__ConsumedMetadata__MaxBytes` in the existing verifier Job templates and values. These paths belong to PR1. To avoid multiple16GiB consumers exceeding phase budget, phase archive aggregate remains authoritative and refuses excess; each capture is bounded by the configured maximum and the phase aggregate is independently enforced during archiving. Consumer max does not silently truncate; sticky gap and prefix retention. No runtime option can raise fixed policy maxima. Existing host options have no production defaults, so explicit chart values and validator coverage required.
- archive returns0 only after all expected files/captures and exact transferred lengths/hashes verify and completion marker readback passes. Returns1 for evidence incomplete/transport gap/bound exhaustion; returns2 for invalid config/unsupported contract. Always write a bounded local archive-manifest.json when enough validated run identity exists. Always cleanup partial local files/processes; workflow delete/destroy remains unconditional. Do not print credential-bearing parser/subprocess bodies.

### 4. Consumed metadata schema and capture rules
One UTF-8 JSON object plus LF per consumed in-window record in poll order. Record is raw facts plus existing decoded projection, not a replacement scientific input:
```text
 {schema_version:1,capture_id,ordinal,topic,partition,offset,
  timestamp:{type:"create_time"|"log_append_time"|"not_available",unix_milliseconds:int|null},
  key_base64:string|null,key:string|null,instance_id:string|null,attempt_id:string|null,sequence:int|null,
  headers:[{name:string,value_base64:string|null}],
  payload:{is_null:bool,length:int|null,sha256:string|null},
  identity_problems:[{field,code}]}
```
Keep duplicate headers and their original order, including unknown names/null/empty values. key_base64=null versus "" retains null/empty distinction; same for header values. Null payload => is_null=true,length=null,sha256=null; empty nonnull payload =>false,0,SHA256(empty). Compute SHA256 for every nonnull payload; never emit full payload. `key`/IDs/sequence use current decoder's strict rules and retain problems, not lossy repair. Kafka timestamp raw milliseconds/type should be retained; no fabricated UTC when unavailable. Use the pinned Confluent.Kafka 2.16.0 Timestamp and Headers API sources cited in the source additions below; preserve their raw values and enumeration order.
`capture_id` is the existing validated mode, verifier Job name and pod UID joined by hyphens; it must satisfy the 1024-character safe-segment bound. Chunk ordinals start at 1; row ordinals start at 0 and are contiguous within the capture. The byte count includes the terminating LF.

`capture.json` has exactly: `schema_version:1`, `capture_id:string`, `identity:{run_id,phase,scenario_id,configuration_id,job_name,pod_uid,source_commit}`, `window:{topic,start:[{partition,offset}],end:[{partition,offset}]}`, `chunks:[{name,bytes,sha256,rows}]`, `observed_rows:int64`, `captured_rows:int64`, `complete:bool`, `first_missing_ordinal:int64|null`, `gaps:[{code,ordinal:int64|null}]`. Watermark vectors are sorted unique partition IDs; nonnegative rows/lengths and lowercase 64-hex SHA256 are required. A complete capture has equal observed/captured counts, no gap and a null missing ordinal. Missing/partial vectors remain an explicit incomplete capture, never a fabricated empty window. Capture gap codes are `row_limit`, `byte_limit`, `write_failed`, `upload_failed`, `interrupted`, `source_window_incomplete`, or `diagnostics_truncated`.

Capture.json is separate last writer statement: identity/window bounds, each chunk path/bytes/SHA256/rows, observed and captured counts, completion, first missing ordinal, gap codes. Zero-row successful window still emits capture.json with zero chunks. Missing capture.json differs from zero rows. Capture a raw record before filtering malformed identity or earlier-repeat ownership; label window/mode so later analysis can apply ownership rules. Records outside assigned [start,end) stay outside this capture; never claim complete topic history. On cancellation, disk/row/chunk/byte-limit error retain sealed chunks and diagnostic state, mark incomplete, continue cleanup; never silently omit oversized rows. Flush/seal any valid partial chunk in finally, then publish capture.json create-only. Capture gap must not alter Kafka scientific status or existing worker completion definition; separate archive readiness holds later verdict publication. At process death unavailable local chunks become explicit gaps.
Check size before base64/string allocation: checked64-bit accounting for UTF8, JSON escaping and base64 expansion, bounded output stream that refuses byte beyond row/chunk/aggregate limit. SHA256 incrementally; raw payload is already owned by Kafka client callback, do not clone it or retain it in metadata. Chunk rotation occurs before appending a row that would exceed128MiB; exact limit accepted including newline. Never split a row. Stream chunks to Blob/local file, not byte[]/MemoryStream of archive size or list of all rows. Capture overhead changes measured system; disclose and smoke its actual configured path.

### 5. Transfer/manifest protocol
Inventory first derives required expected objects from fixed run/phase/repeat/mode/source/image and producer Job registrations/publication references, not only successful listings. Listed files cannot stand in for missing expected ones. Include registration/publication/index/manifest/ledger/statistics/request-lifecycle/capability files; start-line/broker inventory/completions; repeat results/verify-completion; final/sweep inputs/results/completions; candidate/judge-completion; consumed metadata chunks/capture.json; B10 first/successor/report sets above; observed diagnostic outputs and local phase-status/log/pod JSON. Respect active scenario selection: unselected/not-started units are explicitly not_started rather than claimed zero/complete. Preserve already observed invalid/failed evidence without certifying content. Exclude credential/config private files and claim-check full payload blobs from manifest inventory because no full-payload retention was requested. Do not recursively dump run storage or private runner dirs. For each existing non-ledger mode, retain the exact published source inventory named by its current producer/verifier publication contract and every immutable object reference it declares, bounded by the same listing/object rules. A mode without the declared publication index is incomplete; a directory listing alone cannot supply it.
Destination prefix: `v1/<source_commit>/<github_run_id>/<github_run_attempt>/<run_id>/<phase>/`; copies keep `objects/<source-container>/<source-name>` or `runner/<approved relative path>`. Names validated as confined safe relative paths; fixed prefix and identities cannot be supplied by opaque blob names.
One absolute monotonic600s deadline includes bounded enumeration, all attempts, backoff, source hashing, upload and destination readback, manifest/marker. Deadline starts at archive command invocation, independent of expired scientific/B10 orchestration deadline; scientific no-new-work decisions still stand. At most3 end-to-end transfer attempts per object, no nested retry multiplier. Disable SDK native retries when helper controls retries, or explicitly count them against same3 budget. Permanent invalid identity/oversize/hash mismatch are gaps, no retry. Only transient transport failure retries within remaining time. On any exhaustion: record gap and continue best-effort remaining files while budget remains; never request cap raise or replacement run. Deadline stops transfer and reports incomplete, then cleanup.
Per source object: read properties once per attempt; validate integer byte length0..object cap and nonempty ETag before allocating; reserve against phase cap; stream If-Match ETag to private .partial, hash and charge bytes before append, reject both short and overlong body, atomically seal only exact length. Bounded disk+memory, no full read_bytes. Create destination block blob with If-None-Match:* (application create-only; Contributor RBAC does NOT enforce immutability). Read destination properties and stream it back If-Match destination ETag; verify exact length and SHA256. Metadata hash or Content-MD5 alone is not SHA256 content verification. Existing destination can count only after independent byte/hash verification for expected content+identity; conflict blocks and is never overwritten. No mutable latest pointer, no delete old archive to retry.
Exact specified transport implementation: use the existing serialized Azure CLI subprocess as transport; launch each archive download/upload/readback through a POSIX child-only resource.setrlimit(RLIMIT_FSIZE,(268435456,268435456)) helper and existing timeout/process-group cleanup. HEAD contentLength must first be integer0..268435456 with nonempty ETag; source and destination downloads use --if-match ETag, --overwrite false, --max-connections1, --auth-mode login and explicit private .partial file. No shell, no new package, token extraction or credential persistence. A file-limit signal/EFBIG/nonzero exit is a transfer gap. After process success, check exact stat size; open with no-follow semantics and stream SHA256 in64KiB chunks, checking remaining byte budget before each read/hash increment and refusing surplus. This admits at most256MiB resident disk per downloaded object even if transport lies; partial files never accepted. SDK-internal download buffering remains bounded by one object and CLI connection count, never16GiB. Before each object, free-space check for two maximum-object spools plus bounded metadata reserve; failure records disk_space gap before transfer. Keep only source spool and destination readback spool concurrently (maximum512MiB plus control metadata), unlink after digest verification, then next object; never collect whole archive locally. Destination upload uses --if-none-match "*" and --overwrite false, then bounded If-Match readback and SHA256 comparison; upload properties/declared source hash alone never count as verified destination content. For small/zero objects, child cap remains256MiB to permit existing Azure session bookkeeping, while actual stream/stat checks enforce expected exact length before accepting anything. Aggregate16GiB is logical archived bytes, not resident disk. Existing scripts/azure-cli-session/az uses60s invocation policy (source read as necessary dependency from workflow wrapper path), so each process obeys that plus global600s. macOS and Ubuntu POSIX support of resource limits and pinned actual CLI flags must be covered by focused helper tests and live smoke; no performance claim made.
Write canonical archive-manifest.json after all object outcomes known, max1MiB UTF8; if output would exceed reserve, deterministic truncated_inventory gap and no success marker. SHA256 exact written bytes; create-only upload and readback verify manifest. Completion marker `archive-completion.json` <=64KiB is written last only when complete inventory/capture and every object+manifest verified. It binds schema/run/phase/source/images, manifest path/bytes/SHA256 and totals, complete=true; create-only upload and readback verify. On fail, preserve incomplete archive-manifest and transferred prefixes; absent marker means never admissible. After marker write succeeds but local interruption occurs, later receipt must re-read marker + digest-bound manifest; mere existence still insufficient. Failed/incomplete runs can have fully complete archive transfer if their expected diagnostic inventory was captured; scientific completeness is separate. Missing science files known required makes archive incomplete, even if transfer of available files succeeded.
Small GitHub manifest shape (closed schema; field names and nullability are binding):
```text
 {schema_version:1,kind:"run_evidence_archive",archive_complete:bool,
  run:{github_run_id:string,github_run_attempt:int,run_id,scenario_id,configuration_id,phase,source_commit,
       images:{loadgen:string|null,verifier:string|null,probe:string|null},run_kind,purpose,scientific_admissible:bool},
  destination:{account_host,container,prefix,retention_days:90},
  policy:{max_bytes:17179869184,max_object_bytes:268435456,max_objects:1024,deadline_seconds:600,attempts:3,
          metadata_chunk_bytes:134217728,metadata_row_bytes:1048576},
  expected_units:[{repeat:int|null,item:string|null,mode,job_name,pod_name:string|null,pod_uid:string|null,
                   instance_id:string|null,state:"started"|"not_started"}],
  objects:[{kind,source:{container,name,etag:string|null},destination_name,required:bool,
            repeat:int|null,capture_id:string|null,bytes:int|null,sha256:string|null,
            destination_etag:string|null,state:"verified"|"missing"|"rejected"|"failed"|"not_started",gap_code:string|null}],
  captures:[{capture_id,mode,repeat:int|null,required:bool,complete:bool,rows:int|null,first_missing_ordinal:int|null}],
  totals:{expected_objects:int,verified_objects:int,verified_bytes:int},
  gaps:[{code,boundary,object_name:string|null,expected_unit:string|null}],
  completion:{path:string|null,bytes:int|null,sha256:string|null}}
```
Archive gap codes are exactly `config_invalid`, `source_missing`, `capture_missing`, `capture_incomplete`, `source_changed`, `source_too_large`, `source_length_mismatch`, `source_hash_mismatch`, `destination_conflict`, `destination_length_mismatch`, `destination_hash_mismatch`, `manifest_too_large`, `object_limit`, `byte_limit`, `deadline`, `attempts_exhausted`, `disk_space`, `transport`, `interrupted`, `monitoring_incomplete`, and `diagnostics_truncated`. `boundary` names the failed policy/check; it is diagnostic text bounded to 2048 UTF-8 bytes, never a credential-bearing exception dump. No unknown code can authorize completion.

Avoid self-hash: canonical remote manifest always sets completion to null; small GitHub receipt `archive-receipt.json` binds manifest+completion hash/length/path after both verified. GitHub archive-manifest.json can be canonical bytes plus separate receipt. No timestamps pretending to expire exactly90d. No fabricated source resource IDs/credentials. Arrays bounded by fixed1024 inventory, stable sorting and closed bounded gap code list; exhaustive expected inventory must fit1MiB or fail closed.
Later verdict-publication prerequisite: valid completion+digest-bound expected inventory with archive_complete=true for every contributing phase; archival gap holds publication only. Archive failure must not rewrite current candidate scientific status/gate, expected.yaml, cohort classification, or count zeros. Archive gate lives after scientific judgement. Cross-phase dispatch gating/full_allowed should retain prior scientific meaning unless separately authorized.

### 6. Proportional named tests to include (required; not executed in planning)
- `ConsumedMetadata_RawKeyAndOrderedHeadersRoundTrip`: null/empty/binary/invalidUTF8 keys, duplicate+unknown headers/order/null/empty, decoded problems, timestamp unavailable/create/logappend, null/empty payload hashes and no payload member.
- `ConsumedMetadata_RowChunkAndTotalBounds`: row/chunk/aggregate at limit-1,limit,limit+1 with newline/UTF8/base64 expansion; exact chunk rotation; checkedInt64 overflow; huge source rejected before allocation; zero-row window emits complete capture.
- `ConsumedMetadata_PartialDrainRetainsSealedChunks`: offset error, budget exception, cancellation, writer/upload failure; sticky first missing ordinal; scientific assessments/results unchanged. Exercise normal/abrupt/final-sweep common seam.
- `PhasePaths_ConsumedCaptureNamesAreConfined`: all allowed modes and repeated job identities, collision/path traversal/oversized segment rejection, Python/C# literal path parity.
- `test_archive_rejects_invalid_config_before_side_effects`: missing/null/unknown/duplicate JSON members; wrong types including bool-as-int; mismatch account/host/container; malicious URL/path; bad SHA/digest/phase; no calls/files before validation. Keep unrelated fixture fields valid.
- `test_archive_inventory_includes_missing_expected_and_failed_runs`: ordinary + B10 first/successor optionality + metadata zero-row/missing capture; expected absent differs from unselected/not_started; diagnostic trial remains inadmissible scientifically.
- `test_archive_source_etag_and_stream_limits`: missing/changed ETag, short/long stream, metadata/object/aggregate below/at/above, negative/fractional/overflow lengths; bounded reader memory; no full-file read_bytes;1024 object ceiling counts controls.
- `test_archive_destination_conflict_and_sha256_readback`: create-only exact flags, same-length corrupted readback, stale ETag, existing matching blob accepted only after verification, existing conflicting blob never overwritten; retry does not duplicate totals.
- `test_archive_attempts_and_global_deadline`: deterministic fake monotonic time; three attempts maximum, permanent rejection no retry, all work/backoff included, timeout/interruption leaves no success marker and cleanup callable. Test nested transport retries disabled/countable.
- `test_archive_completion_written_last`: object/manifest/readback failure at each stage never accepted; valid canonical manifest hash, marker binds exact identity+length+hash, completion interruption/recovery cannot fabricate success; incomplete archive manifest and transferred prefix retained.
- `test_archive_never_leaks_private_files_or_credentials`: planted sentinels in backend/tfvars/OIDC/token/CLI errors, excluded claim-check payload, no sentinels in public artifact dirs/stdout/stderr; raw spool never beneath uploaded pod directory.
- `test_scenario_archive_before_destroy_on_every_workload_path`: four workflow job paths and byte-identical mirror, always failure path, archive outcome reported, delete/destroy still execute after failure/cancel where runner survives; only small manifest/receipt GitHub paths. No workflow trigger changes.
- tests/terraform/persistent/main.tftest.hcl extend exact local/module input assertions: Hot/LRS/nonpublic/OAuth/keysdisabled/TLS/PEsubnet+DNS/fulltags; explicit no versioning/softdelete; lifecycle creation90 all archive prefixes; existing workflow principal exact container Contributor; no workload archive grants; output exact fields; synthetic fixture disclaimer remains. Do not treat mocked plans as live proof.
Standing existing-fixture rule: preserve every assertion/boundary; fix fixture to satisfy new input checks, or if invalidity is test purpose update expected result to exact check output; list each forced fixture change. No test weakening/removal.

## PR2: native statistics and application retries

### Numbered PR2 requirements and test map

N1. WHEN a supported native scalar is present, THEN apply its exact catalogue row, unit and fixed instrument; preserve nonmetric metadata/raw-only fields in the original report. Test `Given_AllScalarKinds_When_Projected_Then_LiteralCatalogueTargetsMatch` with every row's independent literal target.

N2. WHEN a same-lifetime counter is 2 then 5, THEN emit Adds [2,3]; WHEN it is 5,2,9, THEN emit only [5] and retain quarantine. Test `Given_CounterReset_When_LaterValueRises_Then_QuarantineRemains`.

N3. WHEN a window has zero samples or is absent, THEN apply the window availability contract below. Test `Given_EmptyWindow_When_Projected_Then_OnlyAvailableMetadataEmits` with its literal count/value fixture.

N4. WHEN a signed sentinel, boolean, alias or finite state is present, THEN preserve the exact numeric/encoding policy. Test `Given_NativeSentinelsStatesAndAliases_When_Projected_Then_ExactValuesAreRetained` with the literal vectors below.

N5. WHEN a known field is malformed or admission exceeds a bound, THEN retain the raw report and bounded diagnostics, suppress the specified new projection, and do not partly advance counter state. Test `Given_MalformedProjection_When_NextReportArrives_Then_StateWasNotPartlyAdvanced` with accepted 2, rejected 5, accepted 7 -> [2,5].

N6. WHEN any existing producer/consumer path builds a native handle, THEN wire one monitoring/capture lifetime before Build and preserve its existing evidence callback. Test `Given_EachNativeFactory_When_CallbackFires_Then_EvidenceAndMonitoringReceiveItOnce` for every wiring row below, including early-return branches.

N7. WHEN the registered SDK/exporter processes the fixed replay, THEN check exact registry/point coverage, units, gauge last values, counter deltas and absence of SDK overflow. Test `Given_FixedNativeRegistry_When_Exported_Then_NoInstrumentOrPointIsSilentlyLost`; record baseline/expanded replay measurements once under the common cap.

N8. WHEN application retries are invoked or complete, THEN follow R1-R5 below. Named tests use each retry kind and literal event counts; queued selection alone emits no invocation.

N9. WHEN shutdown or intentional death prevents a final metrics export, THEN distinguish monitoring delivery from raw capture and never fabricate the missing tail. Test `Given_AbruptStop_When_NoTerminalExportExists_Then_RawCaptureAndUnknownOutcomeRemainDistinct`.

### Fixed external metric contract

Add no package, project or assembly reference. Keep meter `Parchwell.Kafka.Statistics`. Create the complete instrument registry once per DI-owned meter; never create instruments from arbitrary JSON field names, entity keys, state values or request strings. All new metric names are literal `kafka.librdkafka.<scope>.<native_field>` names in the appendix. Scope is `client`, `broker`, `topic`, `topic.partition`, `cgrp`, `eos` or `fatal`; remove dictionary wildcards. The request map has one specific instrument `kafka.librdkafka.broker.req` and a bounded `request_type` attribute. This is not a generic field/value instrument. Do not add a statistics-wide `field`, `attempt`, message `key`, document identifier, error text or error-reason dimension.

Register numerical values as synchronous `Gauge<long>` with last-value aggregation and no rolling cache that fabricates an observation when a callback omits a value. Register only the explicit C/Q appendix rows as `Counter<long>`, applying per-client-lifetime deltas. Boolean gauges use 1/0. Do not use histograms for native window min/max/avg/percentile summaries; do not reconstruct native samples or accumulate native window sum/count between reports. Units are exact literals in the appendix. Offset/sequence/identifier units are `1`; bytes are `By`; us/ms/s remain native without conversion. Counts with dissimilar meanings have distinct instrument names even where their units match.

New instruments use exactly common attributes `scenario`, `run`, `role`, `client_id`, `native_client_name`, `client_type`. Role spelling is the existing KafkaClientRole.ToString(). Native client name/type are the validated root fields. Broker instruments additionally use `broker` (dictionary key/name must agree) and `broker_address` only if native nodename is present and valid. Topic instruments add `topic`; partition instruments add `topic` and integer `partition`, including structural UA identity -1. Each state/source instrument adds only its named bounded dimension (`state`, `fetch_state`, `join_state`, `idemp_state`, `txn_state` or `source`); request instrument adds only bounded `request_type`. No broker numeric ID, partition leader, epoch, producer ID or sequence value becomes a dimension: those are gauges. Redundant toppars topic/partition identifiers are structural attributes/raw evidence and need no invented membership measure. Root name/client_id/type and topic/partition structural identities have no redundant numeric instrument. Entity dictionaries and nested windows are containers, not measurements.

Retain these four instruments exactly, including names, instrument types, units, five attributes (`scenario`,`run`,`role`,`client_id`,`broker`), emission and histogram view behavior: `kafka.broker.throttle.max_ms` Histogram<long> ms; `kafka.broker.rtt.p99_us` Histogram<long> us; `kafka.broker.req_timeouts` Counter<long> count; `kafka.broker.disconnects` Counter<long> count. The two counter aliases receive exactly the same already-validated delta as their new canonical instrument; do not advance state twice. The two legacy window instruments emit only if cnt>0. No extra attribute is added to them. Existing source: src/Parchwell.Kafka/Statistics/KafkaStatisticsMetrics.cs and Telemetry/ParchwellTelemetryServiceCollectionExtensions.cs,133-137. Existing tests pin the four-item registry at KafkaStatisticsMetricsTests.cs; adapt this test by retaining exact legacy assertions and adding an independent literal new registry assertion, rather than asserting four total instruments or discarding assertions.

### Projection, absence, validation, and counter lifetime

Parse native JSON only once per worker report where practical, retain original callback string independently, and build a separate immutable monitoring projection. Do not serialize an expanded monitoring model through existing public KafkaStatisticsSnapshot/KafkaBrokerStatistics positional records: the verifier's captured typed-snapshot reader is a separate existing strict wire contract. Keep current selected evidence projection and its existing validation/completion behavior. The new projection owns monitoring diagnostics; it must not expand scientific validity/late-write decision rules implicitly.

Keep the existing callback fast and bounded. Project on its bounded worker, not network/export/Blob I/O in the callback. New projection checks: (1) object root; (2) recursive duplicate-property rejection/depth 64; (3) existing identity/native type/creation epoch/increasing ts and future-clock checks before metric emission; (4) known object/map fields, if present, must have object type; (5) known numeric fields, if present, must be JSON integral Int64, with nonnegative checks for N/C/W and signed values for V; (6) known bool fields must be JSON bool; (7) present identity values must agree with dictionary identities; (8) window count must be present/nonnegative before its value summaries can emit; (9) bounded entity/lifetime tuple admission and fixed allowlists before measurement/state admission. A malformed known field suppresses that callback's new native-metric projection transactionally and retains the original raw report plus a bounded diagnostic; do not advance its counter baselines partially. Existing legacy capture rejects exactly what it already rejects. Unknown fields remain raw and create no instruments; an unknown native state/request value emits a bounded raw diagnostic, never a novel metric tag.

Optional means optional: absent role fields, absent cgrp/eos/fatal, absent topic, absent broker request keys and empty dictionaries emit no measurement and are never filled with zeros. Missing does not reset/remove an existing admitted counter baseline or lift quarantine. A later reappearing counter in the same lifetime continues against the previous value; a decrease quarantines it. Invalid native-client identity/timestamp/lifetime suppresses all emission for that report and preserves the original raw report. No new native lifetime is silently inferred within an already attached session: create a fresh session before each native Build, exactly as existing client_instance_changed behavior requires (KafkaStatisticsSession.cs).

Counter state key = internal capture/client lifetime (native creation epoch + immutable assigned identity), canonical field and structural entity path (plus bounded request opcode when relevant). First value v emits Add(v), next value w>=v emits Add(w-v), equality emits Add(0). A decrease emits no delta for that field and permanently quarantines that field for that lifetime; retain `counter_reset` diagnostic even after a later higher value. Other fields and other clients/lifetimes continue independently. No automatic reset-to-current, negative Add, absolute-as-Add replay, or disappearance-based baseline deletion. Apply the same state to the legacy timeout/disconnect aliases. Only the five existing guard counters retain their present effect on capture completeness; newly added fields' quarantine diagnostics belong to monitoring and do not silently add a scientific validity rule. Existing guard-counter behavior remains (KafkaStatisticsSession.cs,828-866). All C rows, not all native fields documented `int`, obey this policy: timestamps, thresholds, IDs, epochs, sequences and windows are gauges.

Signed V gauges retain native sentinel/identifier/logical-offset values exactly (for example broker nodeid=-1, txidle/rxidle=-1, producer_id=-1, producer_epoch=-1, offset=-1001), without clipping, counter-reset handling or fabricated zero; the metric is the native field value, not a claim the sentinel is a measured lag/position. Raw report records the original meaning/value. Gauge changes, including decreases and negative sentinels, never trigger counter_reset. Values outside Int64 are retained raw with numeric-overflow diagnostic and no projection, rather than lossy casts. Local Gauge<long> values remain exact; backend precision is a named deployment unknown below.

Window W0 fields cnt/outofrange/hdrsize export their native gauge value even with cnt=0. Other W+ fields min/max/avg/sum/stddev/p50/p75/p90/p95/p99/p99_99 emit only when cnt>0, retaining native 0 values if actual samples exist. cnt=0 suppresses all W+ values regardless of native placeholder values. A complete empty window may report W0 zeros; missing cnt is different from cnt=0 and cannot authorize W+ output. Native rollover resets samples per callback (rdavg.h:139-169 and rdkafka.c:1559-1602), so count/sum are never cumulative Counter instruments.

Alias A: canonical committed_offset wins when present. If only commited_offset is present, accept it as the same canonical gauge. If both are present they must be equal; unequal values produce alias_conflict and suppress the new projection transactionally. Never emit a second alias instrument or add the values. Native v2.16.0 emits both names from the same source value (rdkafka.c:1682-1684,1734-1735).

### Finite encodings

Each known state/source observation emits the complete fixed one-hot vector: matching allowlist entry 1, all other entries 0. Unknown value emits the complete existing allowlist vector as zeros and a bounded raw diagnostic; do not add an `unknown` state series, use the raw diagnostic to distinguish unknown/absent. An absent optional state emits no vector. These vectors are gauges with unit `1`; instrument names are literal registry names. Values are case-sensitive; no broad normalization. Current native state arrays override incomplete examples in STATISTICS.md.

- `kafka.librdkafka.broker.state`: `INIT`, `DOWN`, `TRY_CONNECT`, `CONNECT`, `SSL_HANDSHAKE`, `AUTH_LEGACY`, `UP`, `APIVERSION_QUERY`, `AUTH_HANDSHAKE`, `AUTH_REQ`, `REAUTH`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L85.
- `kafka.librdkafka.topic.partition.fetch_state`: `none`, `stopping`, `stopped`, `offset-query`, `offset-wait`, `validate-epoch-wait`, `active`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_partition.c#L41.
- `kafka.librdkafka.cgrp.state`: `init`, `term`, `query-coord`, `wait-coord`, `wait-broker`, `wait-broker-transport`, `up`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_cgrp.c#L332.
- `kafka.librdkafka.cgrp.join_state`: `init`, `wait-join`, `wait-metadata`, `wait-sync`, `wait-assign-call`, `wait-unassign-call`, `wait-unassign-to-complete`, `wait-incr-unassign-to-complete`, `steady`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_cgrp.c#L337.
- `kafka.librdkafka.eos.idemp_state`: `Init`, `Terminate`, `FatalError`, `RequestPID`, `WaitTransport`, `WaitPID`, `Assigned`, `DrainReset`, `DrainBump`, `WaitTxnAbort`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_int.h#L177.
- `kafka.librdkafka.eos.txn_state`: `Init`, `WaitPID`, `ReadyNotAcked`, `Ready`, `InTransaction`, `BeginCommit`, `CommittingTransaction`, `CommitNotAcked`, `BeginAbort`, `AbortingTransaction`, `AbortedNotAcked`, `AbortableError`, `FatalError`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_int.h#L227.
- `kafka.librdkafka.broker.source`: `learned`, `configured`, `internal`, `logical`. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md#L87; emitter https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c#L1991-L1993.

### Bounded request allowlist

The following exact native strings map opcodes 0..79, in order, to the one `broker.req` instrument. Preserve the native key in `request_type`; do not derive an arbitrary tag value. `Unknown-62?`, `Unknown-70?`, `Unknown-73?`, `Unknown-74?`, `Unknown-75?`, `Unknown-77?` are explicitly bounded pinned native fallbacks and are allowed; another `Unknown-<N>?` is not. Missing zero-valued Admin requests/role-filtered keys are absent, not zero. No role-specific request-count reconstruction. Source: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_protocol.h#L50-L127; https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_proto.h#L100-L191; https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c#L1767-L1867.

- 0..9: `Produce`, `Fetch`, `ListOffsets`, `Metadata`, `LeaderAndIsr`, `StopReplica`, `UpdateMetadata`, `ControlledShutdown`, `OffsetCommit`, `OffsetFetch`.
- 10..19: `FindCoordinator`, `JoinGroup`, `Heartbeat`, `LeaveGroup`, `SyncGroup`, `DescribeGroups`, `ListGroups`, `SaslHandshake`, `ApiVersion`, `CreateTopics`.
- 20..29: `DeleteTopics`, `DeleteRecords`, `InitProducerId`, `OffsetForLeaderEpoch`, `AddPartitionsToTxn`, `AddOffsetsToTxn`, `EndTxn`, `WriteTxnMarkers`, `TxnOffsetCommit`, `DescribeAcls`.
- 30..39: `CreateAcls`, `DeleteAcls`, `DescribeConfigs`, `AlterConfigs`, `AlterReplicaLogDirs`, `DescribeLogDirs`, `SaslAuthenticate`, `CreatePartitions`, `CreateDelegationToken`, `RenewDelegationToken`.
- 40..49: `ExpireDelegationToken`, `DescribeDelegationToken`, `DeleteGroups`, `ElectLeadersRequest`, `IncrementalAlterConfigsRequest`, `AlterPartitionReassignmentsRequest`, `ListPartitionReassignmentsRequest`, `OffsetDeleteRequest`, `DescribeClientQuotasRequest`, `AlterClientQuotasRequest`.
- 50..59: `DescribeUserScramCredentialsRequest`, `AlterUserScramCredentialsRequest`, `VoteRequest`, `BeginQuorumEpochRequest`, `EndQuorumEpochRequest`, `DescribeQuorumRequest`, `AlterIsrRequest`, `UpdateFeaturesRequest`, `EnvelopeRequest`, `FetchSnapshot`.
- 60..69: `DescribeCluster`, `DescribeProducers`, `Unknown-62?`, `BrokerHeartbeat`, `UnregisterBroker`, `DescribeTransactions`, `ListTransactions`, `AllocateProducerIds`, `ConsumerGroupHeartbeat`, `ConsumerGroupDescribe`.
- 70..79: `Unknown-70?`, `GetTelemetrySubscriptions`, `PushTelemetry`, `Unknown-73?`, `Unknown-74?`, `Unknown-75?`, `ShareGroupHeartbeat`, `Unknown-77?`, `ShareFetch`, `ShareAcknowledge`.

### Literal field appendix

This is the full 193-template scalar disposition: 188 expanded fixed documented scalar paths + 1 bounded request-map template + 4 native emitter extras. These are schema-template counts measured by the cached catalogue script, not a deployed metric/instrument/series count. The 13 documented object rows only select containers; six expansions of each of the 14 window scalar rows are listed explicitly. Every target below is literal; `*` matches only admitted structural entity keys, never a new instrument name.

Policy: I=structural identity attribute; R=raw-only; C=nonnegative cumulative delta plus sticky quarantine; Q=bounded request allowlist; N=nonnegative gauge; V=signed native-value/sentinel gauge; B=boolean 0/1 gauge; S=fixed one-hot state/source; W0=window count/memory/outofrange gauge even when empty; W+=summary only if cnt>0; A=legacy alias mapped once. All omitted/present-malformed rules above apply. Units are literal; - means no measurement.

For sources use `https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md#L...` or `.../src/rdkafka.c#L...` according to the Source column. Emitter source spans for all table groups: client=rdkafka.c:1892-1918,2162-2183; broker=1935-2027; topic=2056-2076; partition=1608-1761; cgrp=2101-2123; eos=2126-2147; windows=1559-1602; toppars=2029-2041. Native count semantics are checked against emitters rather than blindly treating all documented int fields as counters.

| Native scalar path | Exact target | Kind | Unit | Policy | Source |
|---|---|---|---|---|---|
| name | native_client_name | attribute | - | I | STATISTICS.md#L52 |
| client_id | client_id | attribute | - | I | STATISTICS.md#L53 |
| type | client_type | attribute | - | I | STATISTICS.md#L54 |
| ts | kafka.librdkafka.client.ts | Gauge<long> | us | N | STATISTICS.md#L55 |
| time | kafka.librdkafka.client.time | Gauge<long> | s | N | STATISTICS.md#L56 |
| age | kafka.librdkafka.client.age | Gauge<long> | us | N | STATISTICS.md#L57 |
| replyq | kafka.librdkafka.client.replyq | Gauge<long> | {operation} | N | STATISTICS.md#L58 |
| msg_cnt | kafka.librdkafka.client.msg_cnt | Gauge<long> | {message} | N | STATISTICS.md#L59 |
| msg_size | kafka.librdkafka.client.msg_size | Gauge<long> | By | N | STATISTICS.md#L60 |
| msg_max | kafka.librdkafka.client.msg_max | Gauge<long> | {message} | N | STATISTICS.md#L61 |
| msg_size_max | kafka.librdkafka.client.msg_size_max | Gauge<long> | By | N | STATISTICS.md#L62 |
| tx | kafka.librdkafka.client.tx | Counter<long> delta | {request} | C | STATISTICS.md#L63 |
| tx_bytes | kafka.librdkafka.client.tx_bytes | Counter<long> delta | By | C | STATISTICS.md#L64 |
| rx | kafka.librdkafka.client.rx | Counter<long> delta | {response} | C | STATISTICS.md#L65 |
| rx_bytes | kafka.librdkafka.client.rx_bytes | Counter<long> delta | By | C | STATISTICS.md#L66 |
| txmsgs | kafka.librdkafka.client.txmsgs | Counter<long> delta | {message} | C | STATISTICS.md#L67 |
| txmsg_bytes | kafka.librdkafka.client.txmsg_bytes | Counter<long> delta | By | C | STATISTICS.md#L68 |
| rxmsgs | kafka.librdkafka.client.rxmsgs | Counter<long> delta | {message} | C | STATISTICS.md#L69 |
| rxmsg_bytes | kafka.librdkafka.client.rxmsg_bytes | Counter<long> delta | By | C | STATISTICS.md#L70 |
| simple_cnt | kafka.librdkafka.client.simple_cnt | Gauge<long> | 1 | V | STATISTICS.md#L71 |
| metadata_cache_cnt | kafka.librdkafka.client.metadata_cache_cnt | Gauge<long> | {topic} | N | STATISTICS.md#L72 |
| brokers.*.name | broker | attribute | - | I | STATISTICS.md#L84 |
| brokers.*.nodeid | kafka.librdkafka.broker.nodeid | Gauge<long> | 1 | V | STATISTICS.md#L85 |
| brokers.*.nodename | broker_address | attribute | - | I | STATISTICS.md#L86 |
| brokers.*.source | kafka.librdkafka.broker.source | Gauge<long> one-hot | 1 | S | STATISTICS.md#L87 |
| brokers.*.state | kafka.librdkafka.broker.state | Gauge<long> one-hot | 1 | S | STATISTICS.md#L88 |
| brokers.*.stateage | kafka.librdkafka.broker.stateage | Gauge<long> | us | N | STATISTICS.md#L89 |
| brokers.*.outbuf_cnt | kafka.librdkafka.broker.outbuf_cnt | Gauge<long> | {request} | N | STATISTICS.md#L90 |
| brokers.*.outbuf_msg_cnt | kafka.librdkafka.broker.outbuf_msg_cnt | Gauge<long> | {message} | N | STATISTICS.md#L91 |
| brokers.*.waitresp_cnt | kafka.librdkafka.broker.waitresp_cnt | Gauge<long> | {request} | N | STATISTICS.md#L92 |
| brokers.*.waitresp_msg_cnt | kafka.librdkafka.broker.waitresp_msg_cnt | Gauge<long> | {message} | N | STATISTICS.md#L93 |
| brokers.*.tx | kafka.librdkafka.broker.tx | Counter<long> delta | {request} | C | STATISTICS.md#L94 |
| brokers.*.txbytes | kafka.librdkafka.broker.txbytes | Counter<long> delta | By | C | STATISTICS.md#L95 |
| brokers.*.txerrs | kafka.librdkafka.broker.txerrs | Counter<long> delta | {error} | C | STATISTICS.md#L96 |
| brokers.*.txretries | kafka.librdkafka.broker.txretries | Counter<long> delta | {request} | C | STATISTICS.md#L97 |
| brokers.*.txidle | kafka.librdkafka.broker.txidle | Gauge<long> | us | V | STATISTICS.md#L98 |
| brokers.*.req_timeouts | kafka.librdkafka.broker.req_timeouts | Counter<long> delta | {request} | C | STATISTICS.md#L99 |
| brokers.*.rx | kafka.librdkafka.broker.rx | Counter<long> delta | {response} | C | STATISTICS.md#L100 |
| brokers.*.rxbytes | kafka.librdkafka.broker.rxbytes | Counter<long> delta | By | C | STATISTICS.md#L101 |
| brokers.*.rxerrs | kafka.librdkafka.broker.rxerrs | Counter<long> delta | {error} | C | STATISTICS.md#L102 |
| brokers.*.rxcorriderrs | kafka.librdkafka.broker.rxcorriderrs | Counter<long> delta | {error} | C | STATISTICS.md#L103 |
| brokers.*.rxpartial | kafka.librdkafka.broker.rxpartial | Counter<long> delta | {event} | C | STATISTICS.md#L104 |
| brokers.*.rxidle | kafka.librdkafka.broker.rxidle | Gauge<long> | us | V | STATISTICS.md#L105 |
| brokers.*.zbuf_grow | kafka.librdkafka.broker.zbuf_grow | Counter<long> delta | {event} | C | STATISTICS.md#L107 |
| brokers.*.buf_grow | kafka.librdkafka.broker.buf_grow | Counter<long> delta | {event} | C | STATISTICS.md#L108 |
| brokers.*.wakeups | kafka.librdkafka.broker.wakeups | Counter<long> delta | {event} | C | STATISTICS.md#L109 |
| brokers.*.connects | kafka.librdkafka.broker.connects | Counter<long> delta | {connection} | C | STATISTICS.md#L110 |
| brokers.*.disconnects | kafka.librdkafka.broker.disconnects | Counter<long> delta | {connection} | C | STATISTICS.md#L111 |
| brokers.*.int_latency.min | kafka.librdkafka.broker.int_latency.min | Gauge<long> | us | W+ | STATISTICS.md#L125 |
| brokers.*.outbuf_latency.min | kafka.librdkafka.broker.outbuf_latency.min | Gauge<long> | us | W+ | STATISTICS.md#L125 |
| brokers.*.rtt.min | kafka.librdkafka.broker.rtt.min | Gauge<long> | us | W+ | STATISTICS.md#L125 |
| brokers.*.throttle.min | kafka.librdkafka.broker.throttle.min | Gauge<long> | ms | W+ | STATISTICS.md#L125 |
| topics.*.batchsize.min | kafka.librdkafka.topic.batchsize.min | Gauge<long> | By | W+ | STATISTICS.md#L125 |
| topics.*.batchcnt.min | kafka.librdkafka.topic.batchcnt.min | Gauge<long> | {message} | W+ | STATISTICS.md#L125 |
| brokers.*.int_latency.max | kafka.librdkafka.broker.int_latency.max | Gauge<long> | us | W+ | STATISTICS.md#L126 |
| brokers.*.outbuf_latency.max | kafka.librdkafka.broker.outbuf_latency.max | Gauge<long> | us | W+ | STATISTICS.md#L126 |
| brokers.*.rtt.max | kafka.librdkafka.broker.rtt.max | Gauge<long> | us | W+ | STATISTICS.md#L126 |
| brokers.*.throttle.max | kafka.librdkafka.broker.throttle.max | Gauge<long> | ms | W+ | STATISTICS.md#L126 |
| topics.*.batchsize.max | kafka.librdkafka.topic.batchsize.max | Gauge<long> | By | W+ | STATISTICS.md#L126 |
| topics.*.batchcnt.max | kafka.librdkafka.topic.batchcnt.max | Gauge<long> | {message} | W+ | STATISTICS.md#L126 |
| brokers.*.int_latency.avg | kafka.librdkafka.broker.int_latency.avg | Gauge<long> | us | W+ | STATISTICS.md#L127 |
| brokers.*.outbuf_latency.avg | kafka.librdkafka.broker.outbuf_latency.avg | Gauge<long> | us | W+ | STATISTICS.md#L127 |
| brokers.*.rtt.avg | kafka.librdkafka.broker.rtt.avg | Gauge<long> | us | W+ | STATISTICS.md#L127 |
| brokers.*.throttle.avg | kafka.librdkafka.broker.throttle.avg | Gauge<long> | ms | W+ | STATISTICS.md#L127 |
| topics.*.batchsize.avg | kafka.librdkafka.topic.batchsize.avg | Gauge<long> | By | W+ | STATISTICS.md#L127 |
| topics.*.batchcnt.avg | kafka.librdkafka.topic.batchcnt.avg | Gauge<long> | {message} | W+ | STATISTICS.md#L127 |
| brokers.*.int_latency.sum | kafka.librdkafka.broker.int_latency.sum | Gauge<long> | us | W+ | STATISTICS.md#L128 |
| brokers.*.outbuf_latency.sum | kafka.librdkafka.broker.outbuf_latency.sum | Gauge<long> | us | W+ | STATISTICS.md#L128 |
| brokers.*.rtt.sum | kafka.librdkafka.broker.rtt.sum | Gauge<long> | us | W+ | STATISTICS.md#L128 |
| brokers.*.throttle.sum | kafka.librdkafka.broker.throttle.sum | Gauge<long> | ms | W+ | STATISTICS.md#L128 |
| topics.*.batchsize.sum | kafka.librdkafka.topic.batchsize.sum | Gauge<long> | By | W+ | STATISTICS.md#L128 |
| topics.*.batchcnt.sum | kafka.librdkafka.topic.batchcnt.sum | Gauge<long> | {message} | W+ | STATISTICS.md#L128 |
| brokers.*.int_latency.cnt | kafka.librdkafka.broker.int_latency.cnt | Gauge<long> | {sample} | W0 | STATISTICS.md#L129 |
| brokers.*.outbuf_latency.cnt | kafka.librdkafka.broker.outbuf_latency.cnt | Gauge<long> | {sample} | W0 | STATISTICS.md#L129 |
| brokers.*.rtt.cnt | kafka.librdkafka.broker.rtt.cnt | Gauge<long> | {sample} | W0 | STATISTICS.md#L129 |
| brokers.*.throttle.cnt | kafka.librdkafka.broker.throttle.cnt | Gauge<long> | {sample} | W0 | STATISTICS.md#L129 |
| topics.*.batchsize.cnt | kafka.librdkafka.topic.batchsize.cnt | Gauge<long> | {sample} | W0 | STATISTICS.md#L129 |
| topics.*.batchcnt.cnt | kafka.librdkafka.topic.batchcnt.cnt | Gauge<long> | {sample} | W0 | STATISTICS.md#L129 |
| brokers.*.int_latency.stddev | kafka.librdkafka.broker.int_latency.stddev | Gauge<long> | us | W+ | STATISTICS.md#L130 |
| brokers.*.outbuf_latency.stddev | kafka.librdkafka.broker.outbuf_latency.stddev | Gauge<long> | us | W+ | STATISTICS.md#L130 |
| brokers.*.rtt.stddev | kafka.librdkafka.broker.rtt.stddev | Gauge<long> | us | W+ | STATISTICS.md#L130 |
| brokers.*.throttle.stddev | kafka.librdkafka.broker.throttle.stddev | Gauge<long> | ms | W+ | STATISTICS.md#L130 |
| topics.*.batchsize.stddev | kafka.librdkafka.topic.batchsize.stddev | Gauge<long> | By | W+ | STATISTICS.md#L130 |
| topics.*.batchcnt.stddev | kafka.librdkafka.topic.batchcnt.stddev | Gauge<long> | {message} | W+ | STATISTICS.md#L130 |
| brokers.*.int_latency.hdrsize | kafka.librdkafka.broker.int_latency.hdrsize | Gauge<long> | By | W0 | STATISTICS.md#L131 |
| brokers.*.outbuf_latency.hdrsize | kafka.librdkafka.broker.outbuf_latency.hdrsize | Gauge<long> | By | W0 | STATISTICS.md#L131 |
| brokers.*.rtt.hdrsize | kafka.librdkafka.broker.rtt.hdrsize | Gauge<long> | By | W0 | STATISTICS.md#L131 |
| brokers.*.throttle.hdrsize | kafka.librdkafka.broker.throttle.hdrsize | Gauge<long> | By | W0 | STATISTICS.md#L131 |
| topics.*.batchsize.hdrsize | kafka.librdkafka.topic.batchsize.hdrsize | Gauge<long> | By | W0 | STATISTICS.md#L131 |
| topics.*.batchcnt.hdrsize | kafka.librdkafka.topic.batchcnt.hdrsize | Gauge<long> | By | W0 | STATISTICS.md#L131 |
| brokers.*.int_latency.p50 | kafka.librdkafka.broker.int_latency.p50 | Gauge<long> | us | W+ | STATISTICS.md#L132 |
| brokers.*.outbuf_latency.p50 | kafka.librdkafka.broker.outbuf_latency.p50 | Gauge<long> | us | W+ | STATISTICS.md#L132 |
| brokers.*.rtt.p50 | kafka.librdkafka.broker.rtt.p50 | Gauge<long> | us | W+ | STATISTICS.md#L132 |
| brokers.*.throttle.p50 | kafka.librdkafka.broker.throttle.p50 | Gauge<long> | ms | W+ | STATISTICS.md#L132 |
| topics.*.batchsize.p50 | kafka.librdkafka.topic.batchsize.p50 | Gauge<long> | By | W+ | STATISTICS.md#L132 |
| topics.*.batchcnt.p50 | kafka.librdkafka.topic.batchcnt.p50 | Gauge<long> | {message} | W+ | STATISTICS.md#L132 |
| brokers.*.int_latency.p75 | kafka.librdkafka.broker.int_latency.p75 | Gauge<long> | us | W+ | STATISTICS.md#L133 |
| brokers.*.outbuf_latency.p75 | kafka.librdkafka.broker.outbuf_latency.p75 | Gauge<long> | us | W+ | STATISTICS.md#L133 |
| brokers.*.rtt.p75 | kafka.librdkafka.broker.rtt.p75 | Gauge<long> | us | W+ | STATISTICS.md#L133 |
| brokers.*.throttle.p75 | kafka.librdkafka.broker.throttle.p75 | Gauge<long> | ms | W+ | STATISTICS.md#L133 |
| topics.*.batchsize.p75 | kafka.librdkafka.topic.batchsize.p75 | Gauge<long> | By | W+ | STATISTICS.md#L133 |
| topics.*.batchcnt.p75 | kafka.librdkafka.topic.batchcnt.p75 | Gauge<long> | {message} | W+ | STATISTICS.md#L133 |
| brokers.*.int_latency.p90 | kafka.librdkafka.broker.int_latency.p90 | Gauge<long> | us | W+ | STATISTICS.md#L134 |
| brokers.*.outbuf_latency.p90 | kafka.librdkafka.broker.outbuf_latency.p90 | Gauge<long> | us | W+ | STATISTICS.md#L134 |
| brokers.*.rtt.p90 | kafka.librdkafka.broker.rtt.p90 | Gauge<long> | us | W+ | STATISTICS.md#L134 |
| brokers.*.throttle.p90 | kafka.librdkafka.broker.throttle.p90 | Gauge<long> | ms | W+ | STATISTICS.md#L134 |
| topics.*.batchsize.p90 | kafka.librdkafka.topic.batchsize.p90 | Gauge<long> | By | W+ | STATISTICS.md#L134 |
| topics.*.batchcnt.p90 | kafka.librdkafka.topic.batchcnt.p90 | Gauge<long> | {message} | W+ | STATISTICS.md#L134 |
| brokers.*.int_latency.p95 | kafka.librdkafka.broker.int_latency.p95 | Gauge<long> | us | W+ | STATISTICS.md#L135 |
| brokers.*.outbuf_latency.p95 | kafka.librdkafka.broker.outbuf_latency.p95 | Gauge<long> | us | W+ | STATISTICS.md#L135 |
| brokers.*.rtt.p95 | kafka.librdkafka.broker.rtt.p95 | Gauge<long> | us | W+ | STATISTICS.md#L135 |
| brokers.*.throttle.p95 | kafka.librdkafka.broker.throttle.p95 | Gauge<long> | ms | W+ | STATISTICS.md#L135 |
| topics.*.batchsize.p95 | kafka.librdkafka.topic.batchsize.p95 | Gauge<long> | By | W+ | STATISTICS.md#L135 |
| topics.*.batchcnt.p95 | kafka.librdkafka.topic.batchcnt.p95 | Gauge<long> | {message} | W+ | STATISTICS.md#L135 |
| brokers.*.int_latency.p99 | kafka.librdkafka.broker.int_latency.p99 | Gauge<long> | us | W+ | STATISTICS.md#L136 |
| brokers.*.outbuf_latency.p99 | kafka.librdkafka.broker.outbuf_latency.p99 | Gauge<long> | us | W+ | STATISTICS.md#L136 |
| brokers.*.rtt.p99 | kafka.librdkafka.broker.rtt.p99 | Gauge<long> | us | W+ | STATISTICS.md#L136 |
| brokers.*.throttle.p99 | kafka.librdkafka.broker.throttle.p99 | Gauge<long> | ms | W+ | STATISTICS.md#L136 |
| topics.*.batchsize.p99 | kafka.librdkafka.topic.batchsize.p99 | Gauge<long> | By | W+ | STATISTICS.md#L136 |
| topics.*.batchcnt.p99 | kafka.librdkafka.topic.batchcnt.p99 | Gauge<long> | {message} | W+ | STATISTICS.md#L136 |
| brokers.*.int_latency.p99_99 | kafka.librdkafka.broker.int_latency.p99_99 | Gauge<long> | us | W+ | STATISTICS.md#L137 |
| brokers.*.outbuf_latency.p99_99 | kafka.librdkafka.broker.outbuf_latency.p99_99 | Gauge<long> | us | W+ | STATISTICS.md#L137 |
| brokers.*.rtt.p99_99 | kafka.librdkafka.broker.rtt.p99_99 | Gauge<long> | us | W+ | STATISTICS.md#L137 |
| brokers.*.throttle.p99_99 | kafka.librdkafka.broker.throttle.p99_99 | Gauge<long> | ms | W+ | STATISTICS.md#L137 |
| topics.*.batchsize.p99_99 | kafka.librdkafka.topic.batchsize.p99_99 | Gauge<long> | By | W+ | STATISTICS.md#L137 |
| topics.*.batchcnt.p99_99 | kafka.librdkafka.topic.batchcnt.p99_99 | Gauge<long> | {message} | W+ | STATISTICS.md#L137 |
| brokers.*.int_latency.outofrange | kafka.librdkafka.broker.int_latency.outofrange | Gauge<long> | {sample} | W0 | STATISTICS.md#L138 |
| brokers.*.outbuf_latency.outofrange | kafka.librdkafka.broker.outbuf_latency.outofrange | Gauge<long> | {sample} | W0 | STATISTICS.md#L138 |
| brokers.*.rtt.outofrange | kafka.librdkafka.broker.rtt.outofrange | Gauge<long> | {sample} | W0 | STATISTICS.md#L138 |
| brokers.*.throttle.outofrange | kafka.librdkafka.broker.throttle.outofrange | Gauge<long> | {sample} | W0 | STATISTICS.md#L138 |
| topics.*.batchsize.outofrange | kafka.librdkafka.topic.batchsize.outofrange | Gauge<long> | {sample} | W0 | STATISTICS.md#L138 |
| topics.*.batchcnt.outofrange | kafka.librdkafka.topic.batchcnt.outofrange | Gauge<long> | {sample} | W0 | STATISTICS.md#L138 |
| brokers.*.toppars.*.topic | topic | attribute | - | I | STATISTICS.md#L147 |
| brokers.*.toppars.*.partition | partition | attribute | - | I | STATISTICS.md#L148 |
| topics.*.topic | topic | attribute | - | I | STATISTICS.md#L154 |
| topics.*.age | kafka.librdkafka.topic.age | Gauge<long> | ms | N | STATISTICS.md#L155 |
| topics.*.metadata_age | kafka.librdkafka.topic.metadata_age | Gauge<long> | ms | N | STATISTICS.md#L156 |
| topics.*.partitions.*.partition | partition | attribute | - | I | STATISTICS.md#L166 |
| topics.*.partitions.*.broker | kafka.librdkafka.topic.partition.broker | Gauge<long> | 1 | V | STATISTICS.md#L167 |
| topics.*.partitions.*.leader | kafka.librdkafka.topic.partition.leader | Gauge<long> | 1 | V | STATISTICS.md#L168 |
| topics.*.partitions.*.desired | kafka.librdkafka.topic.partition.desired | Gauge<long> | 1 | B | STATISTICS.md#L169 |
| topics.*.partitions.*.unknown | kafka.librdkafka.topic.partition.unknown | Gauge<long> | 1 | B | STATISTICS.md#L170 |
| topics.*.partitions.*.msgq_cnt | kafka.librdkafka.topic.partition.msgq_cnt | Gauge<long> | {message} | N | STATISTICS.md#L171 |
| topics.*.partitions.*.msgq_bytes | kafka.librdkafka.topic.partition.msgq_bytes | Gauge<long> | By | N | STATISTICS.md#L172 |
| topics.*.partitions.*.xmit_msgq_cnt | kafka.librdkafka.topic.partition.xmit_msgq_cnt | Gauge<long> | {message} | N | STATISTICS.md#L173 |
| topics.*.partitions.*.xmit_msgq_bytes | kafka.librdkafka.topic.partition.xmit_msgq_bytes | Gauge<long> | By | N | STATISTICS.md#L174 |
| topics.*.partitions.*.fetchq_cnt | kafka.librdkafka.topic.partition.fetchq_cnt | Gauge<long> | {message} | N | STATISTICS.md#L175 |
| topics.*.partitions.*.fetchq_size | kafka.librdkafka.topic.partition.fetchq_size | Gauge<long> | By | N | STATISTICS.md#L176 |
| topics.*.partitions.*.fetch_state | kafka.librdkafka.topic.partition.fetch_state | Gauge<long> one-hot | 1 | S | STATISTICS.md#L177 |
| topics.*.partitions.*.query_offset | kafka.librdkafka.topic.partition.query_offset | Gauge<long> | 1 | V | STATISTICS.md#L178 |
| topics.*.partitions.*.next_offset | kafka.librdkafka.topic.partition.next_offset | Gauge<long> | 1 | V | STATISTICS.md#L179 |
| topics.*.partitions.*.app_offset | kafka.librdkafka.topic.partition.app_offset | Gauge<long> | 1 | V | STATISTICS.md#L180 |
| topics.*.partitions.*.stored_offset | kafka.librdkafka.topic.partition.stored_offset | Gauge<long> | 1 | V | STATISTICS.md#L181 |
| topics.*.partitions.*.stored_leader_epoch | kafka.librdkafka.topic.partition.stored_leader_epoch | Gauge<long> | 1 | V | STATISTICS.md#L182 |
| topics.*.partitions.*.committed_offset | kafka.librdkafka.topic.partition.committed_offset | Gauge<long> | 1 | V | STATISTICS.md#L183 |
| topics.*.partitions.*.committed_leader_epoch | kafka.librdkafka.topic.partition.committed_leader_epoch | Gauge<long> | 1 | V | STATISTICS.md#L184 |
| topics.*.partitions.*.eof_offset | kafka.librdkafka.topic.partition.eof_offset | Gauge<long> | 1 | V | STATISTICS.md#L185 |
| topics.*.partitions.*.lo_offset | kafka.librdkafka.topic.partition.lo_offset | Gauge<long> | 1 | V | STATISTICS.md#L186 |
| topics.*.partitions.*.hi_offset | kafka.librdkafka.topic.partition.hi_offset | Gauge<long> | 1 | V | STATISTICS.md#L187 |
| topics.*.partitions.*.ls_offset | kafka.librdkafka.topic.partition.ls_offset | Gauge<long> | 1 | V | STATISTICS.md#L188 |
| topics.*.partitions.*.consumer_lag | kafka.librdkafka.topic.partition.consumer_lag | Gauge<long> | 1 | V | STATISTICS.md#L189 |
| topics.*.partitions.*.consumer_lag_stored | kafka.librdkafka.topic.partition.consumer_lag_stored | Gauge<long> | 1 | V | STATISTICS.md#L190 |
| topics.*.partitions.*.leader_epoch | kafka.librdkafka.topic.partition.leader_epoch | Gauge<long> | 1 | V | STATISTICS.md#L191 |
| topics.*.partitions.*.txmsgs | kafka.librdkafka.topic.partition.txmsgs | Counter<long> delta | {message} | C | STATISTICS.md#L192 |
| topics.*.partitions.*.txbytes | kafka.librdkafka.topic.partition.txbytes | Counter<long> delta | By | C | STATISTICS.md#L193 |
| topics.*.partitions.*.rxmsgs | kafka.librdkafka.topic.partition.rxmsgs | Counter<long> delta | {message} | C | STATISTICS.md#L194 |
| topics.*.partitions.*.rxbytes | kafka.librdkafka.topic.partition.rxbytes | Counter<long> delta | By | C | STATISTICS.md#L195 |
| topics.*.partitions.*.msgs | kafka.librdkafka.topic.partition.msgs | Counter<long> delta | {message} | C | STATISTICS.md#L196 |
| topics.*.partitions.*.rx_ver_drops | kafka.librdkafka.topic.partition.rx_ver_drops | Counter<long> delta | {message} | C | STATISTICS.md#L197 |
| topics.*.partitions.*.msgs_inflight | kafka.librdkafka.topic.partition.msgs_inflight | Gauge<long> | {message} | N | STATISTICS.md#L198 |
| topics.*.partitions.*.next_ack_seq | kafka.librdkafka.topic.partition.next_ack_seq | Gauge<long> | 1 | V | STATISTICS.md#L199 |
| topics.*.partitions.*.next_err_seq | kafka.librdkafka.topic.partition.next_err_seq | Gauge<long> | 1 | V | STATISTICS.md#L200 |
| topics.*.partitions.*.acked_msgid | kafka.librdkafka.topic.partition.acked_msgid | Gauge<long> | 1 | V | STATISTICS.md#L201 |
| cgrp.state | kafka.librdkafka.cgrp.state | Gauge<long> one-hot | 1 | S | STATISTICS.md#L207 |
| cgrp.stateage | kafka.librdkafka.cgrp.stateage | Gauge<long> | ms | N | STATISTICS.md#L208 |
| cgrp.join_state | kafka.librdkafka.cgrp.join_state | Gauge<long> one-hot | 1 | S | STATISTICS.md#L209 |
| cgrp.rebalance_age | kafka.librdkafka.cgrp.rebalance_age | Gauge<long> | ms | N | STATISTICS.md#L210 |
| cgrp.rebalance_cnt | kafka.librdkafka.cgrp.rebalance_cnt | Counter<long> delta | {rebalance} | C | STATISTICS.md#L211 |
| cgrp.rebalance_reason | - | raw-only | - | R | STATISTICS.md#L212 |
| cgrp.assignment_size | kafka.librdkafka.cgrp.assignment_size | Gauge<long> | {partition} | N | STATISTICS.md#L213 |
| eos.idemp_state | kafka.librdkafka.eos.idemp_state | Gauge<long> one-hot | 1 | S | STATISTICS.md#L220 |
| eos.idemp_stateage | kafka.librdkafka.eos.idemp_stateage | Gauge<long> | ms | N | STATISTICS.md#L221 |
| eos.txn_state | kafka.librdkafka.eos.txn_state | Gauge<long> one-hot | 1 | S | STATISTICS.md#L222 |
| eos.txn_stateage | kafka.librdkafka.eos.txn_stateage | Gauge<long> | ms | N | STATISTICS.md#L223 |
| eos.txn_may_enq | kafka.librdkafka.eos.txn_may_enq | Gauge<long> | 1 | B | STATISTICS.md#L224 |
| eos.producer_id | kafka.librdkafka.eos.producer_id | Gauge<long> | 1 | V | STATISTICS.md#L225 |
| eos.producer_epoch | kafka.librdkafka.eos.producer_epoch | Gauge<long> | 1 | V | STATISTICS.md#L226 |
| eos.epoch_cnt | kafka.librdkafka.eos.epoch_cnt | Counter<long> delta | {assignment} | C | STATISTICS.md#L227 |
| brokers.*.req.* | kafka.librdkafka.broker.req | Counter<long> delta | {request} | C/Q | STATISTICS.md#L106; rdkafka.c#L1767-L1867 |
| topics.*.partitions.*.commited_offset | kafka.librdkafka.topic.partition.committed_offset | alias; Gauge<long> | 1 | A | rdkafka.c#L1682-L1684,L1734-L1735 |
| fatal.error | - | raw-only | - | R | rdkafka.c#L2150-L2158 |
| fatal.reason | - | raw-only | - | R | rdkafka.c#L2150-L2158 |
| fatal.cnt | kafka.librdkafka.fatal.cnt | Counter<long> delta | {error} | C | rdkafka.c#L2150-L2158 |

### Client wiring and allowed change paths

Shared library existing files read: src/Parchwell.Kafka/Statistics/{KafkaStatisticsMetrics.cs,KafkaStatisticsSession.cs,KafkaStatisticsParser.cs,KafkaStatisticsSnapshot.cs,KafkaBrokerStatistics.cs,KafkaWindowStatistics.cs,KafkaStatisticsReport.cs,KafkaStatisticsOptions.cs,KafkaStatisticsBridgeFactory.cs,KafkaStatisticsServiceCollectionExtensions.cs,KafkaStatisticsIdentity.cs,KafkaStatisticsDiagnostic.cs,KafkaStatisticsCompletion.cs,IKafkaStatisticsEvidenceSink.cs}; src/Parchwell.Kafka/Telemetry/ParchwellTelemetryServiceCollectionExtensions.cs. Existing parser only projects six root identity/timestamp fields and selected broker fields (KafkaStatisticsParser.cs,119-188). Extend via a new internal bounded monitoring projection/registry, preserving the evidence records' positional/wire shape and current guards. The original raw retention pipeline is assigned separately; monitoring must consume the same admitted original report without rewriting/normalizing it.

Possible private implementation layout (not an external contract): Statistics/KafkaNativeMetricCatalogue.cs (literal descriptor registry and finite allowlists), KafkaNativeMetricProjection.cs (immutable canonical values), KafkaNativeMetricParser.cs (bounded presence/type/value projection), KafkaNativeMetricState.cs (field/entity/lifetime delta state). Reuse KafkaStatisticsMetrics for the actual instruments. Shared internal API shape: parse result with projection + bounded monitoring diagnostics, then metric state Observe(projection) -> typed pending emissions, then Record(emissions); no per-field mutation until whole projection validation/admission succeeds. The capture worker validates identity/lifetime first and invokes this once per report. Expose a DI-created per-native-client monitoring/capture lifetime through the shared registration for callback paths lacking KafkaStatisticsSession; use PR1's sink/writer integration, not by creating a second raw capture or an unbounded observer. Existing bridge must not emit old+new counters a second time through a second monitoring observer.

Wire one composite statistics handler BEFORE Build at each extant client path, sending the unmodified callback report once to existing scenario evidence logic and once to the shared monitoring/capture lifetime (or once total when existing bridge already owns both). Retain the existing native statistics interval unchanged. Do not enable a new interval to manufacture samples in short existing jobs. Shared callback admission/flush cannot become a new verdict classifier. Register the shared meter/lifetime in each corresponding host, keep host-owned providers alive through native cleanup, and dispose each monitoring lifetime after its native client so final callbacks remain admissible. Do not start a new Kafka client for monitoring.

1. Standard loadgen: Program.cs already registers the sink/bridge; Program.cs already calls statistics.Capture. Run/Execution/RunSessionFactory.cs creates a fresh session before producer construction. Extend that same session; no second callback, new observer or duplicate legacy emission.
2. B10 abrupt: Run/Abrupt/B10AbruptHost.cs registers the existing bridge and :214-221 builds a statistics session/handler. Preserve StatisticsHandler at :239-240 exactly in ordering: observations.CaptureNative(raw) first, statistics.Capture(raw) second. Extend same bridge; do not attach an extra monitor to the native handler. Child kill behavior and successor completion order (:242-249) stay as they are.
3. SingleAction producer AND consumer: SingleAction/SingleActionSessionFactory.cs composite handler preserves capture.Statistics first and shared capture/monitor second. Host SingleAction/SingleActionHost.cs registers/passes the shared factory. Native client settings['client.id'], options.ScenarioId/RunId and SelectedRole supply real attribution. Session creation is per native Build, not one state shared across repeats.
4. V2 native-auth producer, application-auth producer AND application-auth consumer: SingleActionV2/ProbeV2SessionFactory.cs,30-32,44-46, with shared registration in SingleActionV2/ProbeV2Host.cs. Cover the early-return NativeAuthentication producer branch as well as standard auth; keep auth-observation/refresh semantics unchanged. Each Build has fresh counter state; do not use a static dictionary keyed just by options.PodName/client.id.
5. IdleObservation producer: IdleObservation/IdleObservationSessionFactory.cs; host IdleObservation/IdleObservationHost.cs. Preserve capture.Statistics callback and idle protocol interval/time windows.
6. Verifier real consumer: Common/Kafka/VerifierKafkaSessionFactory.cs creates an auth consumer but has no statistics callback. Create the shared per-client lifetime before line30, set a statistics callback before Build, retain it in VerifierKafkaSession, and dispose it after existing synchronous admin/consumer disposal in Common/Kafka/VerifierKafkaSession.cs. Register its factory in Program.cs alongside the real Kafka factory, rather than in judge-only modes that create no client. Attribution is settings.ScenarioId/RunId, KafkaClientRole.VerifierConsumer and settings.VerifierClientId. Preserve consumer cleanup watchdog, exception collection and cancellation behavior.
7. Existing enrichment F04 seed producer only: Program.cs has the real producer build, no statistics handler and no telemetry provider registration. Add shared registration/provider identity from existing SeedF04 RunId/PodName and F04 scenario, then attach the shared lifetime/handler before :130 Build. SeedF04/SeedF04Worker.cs owns flush/dispose; keep lifetime through native disposal and drain afterward within its existing cleanup budget. SeedF04Options currently has no telemetry destination; supplying it through the common plan's chart/environment contract is a named deployment prerequisite. There is no extant ordinary enrichment-consumer worker in this entry point; do not invent one as part of #494.

The strict optional telemetry validation-only paths must continue constructing no real clients/network exporters. The DI-host-owned per-client lifetimes from PR1 are authoritative;, the callbacks use that exact factory rather than an independently implemented alternate monitoring path.

### Tests and literal expected values

Existing read tests include KafkaStatistics{Parser,Metrics,Counter,Boundary,Lifetime,Session,DependencyInjection}Tests and statistics fixtures/support; Telemetry/{ParchwellTelemetryExportTests,ParchwellTelemetryRegistrationTests,TelemetryTestSupport.cs}. Keep existing late-capture and capture-completion semantics; their tests are regression inputs when session processing is touched. Existing metric recorder handles only histograms and sums (TelemetryTestSupport.cs); add LongGauge recording so SDK tests assert actual exported last values rather than merely seeing instrument publication.

Add a literal expected catalogue test independent of production descriptors: every appendix measurement name/kind/unit, exactly one canonical alias, six-times-14 window expansion, fixed state/source allowlists, and request 80-key allowlist. Check each scalar synthetic fixture's expected measurement/tag target; identity/raw-only paths produce no extra metric. Do not assert only that registry row count equals 193: it is template coverage, not a metric count.

- Counter case: input client tx=2 then5, same accepted native lifetime and increasing timestamps, must produce Adds exactly [2,3] on kafka.librdkafka.client.tx. Broker req_timeouts=2 then5 produces [2,3] on both old and new aliases with exactly two native observations, no double increment. Exercise one field from each cumulative scope (client, broker, partition, cgrp, eos, fatal, bounded request); expected local deltas [2,3] throughout. Gauges supplied 2 then5 emit [2,5], never [2,3].
- Reset case: same counter values 5,2,9 => emitted Adds [5] only; reset diagnostic stays after 9. Unaffected sibling counter 1,3,6 => [1,2,3]. New native-client session starts 2 => [2], regardless of previous lifetime quarantine. Missing middle field between 2 and 5 still gives [2,3].
- Empty native window: cnt=0,min=999,max=999,sum=999,p99=999,outofrange=0,hdrsize=128 yields only cnt=0/outofrange=0/hdrsize=128 new gauges, no summaries and no legacy window histogram. Next callback cnt=2,min=0,max=12,avg=6,sum=12,p99=12 yields native gauges exactly0,12,6,12,12 with no cross-report sum. Missing window/cgrp/eos/fatal emits none.
- Signed fields: nodeid=-1, producer_id=-1, query_offset=-1001, leader_epoch=-1, broker.txidle=-1, next_ack_seq falling 5->1 all remain native-value Gauge<long> observations and cause no reset. Boolean false/true gives 0/1. Alias equal 42/42 yields one committed_offset=42 observation; alias-only=42 yields same; conflict 42/43 yields no new projection plus alias_conflict, original bytes unchanged.
- State UP->DOWN: broker.state vector UP=1/DOWN=0 then UP=0/DOWN=1, with all other 9 entries0 both times. Unknown `NEXT_VERSION` yields all existing 11 entries0, fixed diagnostic, no extra series. Missing state yields no vector. Test each finite source/state table's endpoint and a rejected arbitrary value. Native request `ElectLeadersRequest` and fixed `Unknown-62?` are accepted exact keys; arbitrary `ExplodeMetricCardinality` produces raw diagnostic and zero new series.
- Malformed retention: duplicate property, wrong known field type, fractional int, Int64 overflow, wrong dictionary identity and malformed JSON all preserve the original received string exactly in raw pipeline. No transactionally rejected new projection advances state; accepted value 2, rejected malformed 5, accepted 7 must yield [2,5], not [2,3,2]. Existing capture-specific validity behavior remains separately asserted.
- Provider/SDK assertions (three): (1) registered production pipeline exposes exact old/new instrument types/units and no dynamically synthesized names; (2) a bounded synthetic identity/entity fixture exports the exact expected points and enforcing configured per-field stream/cardinality capacity reports overflow before losing admitted existing points; (3) varying arbitrary state/request/free-text inputs does not increase instrument/series count and no `attempt`, `key`, `reason`, error prose or generic `field` attribute appears. Inspect the OpenTelemetry SDK export points via TelemetryTestPipeline; MeterListener alone cannot prove SDK stream/cardinality or gauge aggregation behavior. Counter 2->5 SDK cumulative sum is 5 (or delta reader intervals 2,3); native gauge last value is 5. Existing histogram min/max and old tag assertions remain verbatim.
- Wiring tests: invoke each concrete handler with one exact callback string and assert existing scenario capture and shared pipeline each receive that same reference/value exactly once; native-auth V2 branch included. A standard/B10 callback produces one canonical+compatibility metric set, not two. For verifier/enrichment new handler attribution, assert exact role/client/run/scenario and lifetime isolation across two Builds. Host validation-only modes instantiate no native client or exporter.

Standing fixture rule: when a new check rejects an existing fixture, either make that fixture satisfy the new check while retaining every assertion and exercised boundary, or keep intentionally invalid input and update expected values exactly to this contract. No removed test case, loosened assertion or changed check; implementation notes list fixture and forcing check.

### Cardinality and deployment assumptions

Use the fixed entity/client lifetime limits below and validate cardinality against configured SDK stream and per-field point capacities before activation. The literal appendix creates exactly 180 new instruments (147 gauges including 7 one-hot instruments, 33 counters), plus the 4 retained legacy instruments, for 184 instruments on Parchwell.Kafka.Statistics. This is a schema-derived instrument count, not a deployed series count. It is below 1000 native instrument streams, but validate the configured capacity and include non-native meters already registered. A chosen 2000-point SDK cardinality limit is not proof that every native field is fully exportable. Broker/source and six state vectors multiply per-entity series; request keys have at most 80 values. For each process and field, sum actual admitted native client lifetimes and structural identities, times that field's finite vector size when relevant. Native report interval does not multiply series. Preserve raw reports and report overflow explicitly; do not silently drop dimensions, merge clients or silently rely on SDK overflow. Per native capture lifetime, admit at most 64 brokers, 3 topics, 33 partition objects per topic (including -1), and the fixed 80 request keys per broker. Per provider, admit at most 32 native lifetimes and 2000 attribute tuples per new instrument. Validate before recording; exceeding a bound records a sticky monitoring-coverage gap and suppresses excess tuples, never merges clients into an overflow series. The publication-completeness record exposes the gap. These are policy bounds for approval, not observed topology or a guarantee of backend capacity. Include existing subscribed meters when checking the 1000-stream SDK default. Keep that SDK default; set no undocumented exporter option.

Known unknowns (not tested by these offline reads): (a) Application Insights/Azure Monitor preserves all chosen metric names, units, number of dimensions and points at deployed exporter/runtime version; (b) high Int64 numeric identifiers may round in backend representations beyond 2^53 even though local Gauge<long> records exact values; (c) the extant seed job gets a valid AI destination/identity and can export before its short existing process lifetime, without changing its observation protocol; (d) live role/optional field population differs between producer and assigned/subscribed consumer, and short jobs may legitimately emit zero callbacks. Evidence for all is source inspection only in this planning session, with no already-open or fresh live connection and neither idle nor under-load observation. Mark them as unverified live assumptions and apply the common plan's named-risk decision rules. Do not claim all 193 templates become deployed metrics. No live run, extra sampling, native statistics interval change or backend query was performed in planning.

### Application-retry telemetry contract

Use the existing `Parchwell.Workers` meter. Add `parchwell.loadgen.application_retry.events`, `Counter<long>`, unit `count`. Common tags are `scenario`, `run`, `role`, `client_id`; add only `retry_kind` and `event` below. There are no attempt/key/sequence/predecessor/error-message metric dimensions.

`retry_kind` is exactly `timeout`, `same_instance_control`, or `cross_restart_successor`. `event` is exactly `invoked`, `accepted`, `synchronous_rejected`, `terminal_delivered`, `terminal_failed`, or `invocation_exception`. These are transition events; never sum them as mutually exclusive final outcomes or subtract exported counters to assert an unknown outcome.

R1. WHEN the ordinary retry queue selects a retry, THEN count nothing until its actual `Produce` invocation. Pass explicit retry context from `TryAdmitRetries` into the invocation; do not infer retry kind from an attempt-ID string or a null schedule timestamp.

R2. WHEN an application resend actually calls `Produce`, THEN increment `invoked` immediately before the call, `accepted` only after successful return, and one terminal event only after existing callback deduplication. A callback may occur before `Produce` returns; tests permit terminal-before-accepted ordering.

R3. WHEN `ProduceException` is thrown synchronously, THEN record `synchronous_rejected` and preserve the existing path's record-or-rethrow behavior. Other invocation exceptions emit `invocation_exception` and retain existing propagation. Never manufacture a delivery callback or turn an application exception into a measured Kafka delivery failure.

R4. WHEN B10 performs same-process control or fresh-successor resends, THEN instrument their direct calls in `Run/Abrupt/B10AbruptProducer.cs`, which bypass `RunExecutor`. Preserve predecessor bookkeeping, child self-kill and successor protocol. Legacy restart-plan invocations, if still used, are explicitly cross-restart resends. Originals are not retries merely because a successor might later resend them.

R5. WHEN an accepted attempt has no terminal report, THEN retain unknown outcome in journal/manifest evidence; never emit `terminal_failed` or infer a final state from the exporter tail. No Azure export flush is promised after intentional SIGKILL.

Literal tests: queued-but-limit-rejected retry emits zero events; one successful retry emits invoked=1, accepted=1, terminal_delivered=1; synchronous rejection emits invoked=1, synchronous_rejected=1, accepted=0 and no terminal; invocation exception emits invoked=1, invocation_exception=1; duplicate delivery callback increments terminal once; accepted/no terminal has accepted=1 and both terminal counters=0 without a failed outcome. Repeat for timeout, matched control and successor direct paths. All inputs are synthetic; existing ledger/protocol assertions remain.

Allowed additional PR2 paths: `src/Parchwell.LoadGen/Run/Execution/{LoadGenTelemetry.cs,RunExecutor.cs}`, `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs`, their existing test folders, and the telemetry/statistics contracts. No application retry policy, count, timing or scenario setting changes.

## PR3: inline duplicate breakdown

### Goal and exclusions
Add a decomposition of the existing excess-duplicate count to existing verifier artifacts and their readers, serializers, tests and publication contract. Cover every message-counting scenario and both supported B10 finalization paths, including scientific timeout and same_instance/cross_restart probe cohorts. Single-action paths whose message counts are not applicable retain absence, never a fabricated zero. Preserve all four outcome classes, reorder meaning, validity/completion/unknown-outcome gates and existing scientific decision rules. Do not modify results/, expected.yaml, frozen historical evidence/reproduction scripts or verdicts, and do not rerun the published B10 verdict. This change is preparation for future results.

### Meaning
An observation belongs to its checked run/repeat/cohort first. Validate identity and reconciliation under the existing path before calling the helper; missing or malformed instance-id, attempt-id, key or sequence never become an ordinary grouping value. Within the authorized cohort deduplicate physical records by (topic, partition, offset), then group by ordinal (key, sequence). Let N be the distinct physical copies in a nonempty logical group and A the distinct ordinal (instance_id, attempt_id) pairs in those copies. Its existing excess-duplicate total is N-1; duplicate_same_attempt=N-A; duplicate_different_attempt=A-1. Empty input is 0/0/0. Sum each measure across logical groups with checked Int64 arithmetic. Different instance IDs with the same bare attempt ID are different attempts. Different partitions with the same numeric offset are different physical records. Topic belongs to the physical identity, while logical grouping retains the existing key/sequence meaning. A repeated Kafka coordinate contributes once wherever the existing scientific path permits that reread. Do not turn the legacy B10LateWriteChecks repeated-coordinate invalidity into a successful classification: retain its gate and test it as invalid/counts absent. Scientific B10FinalClassification already deduplicates its observation union.

These names describe recorded identity. Same-attempt copies do not prove a librdkafka retry caused them; different-attempt copies do not prove an application resend caused the extra append. No source claim about internal producer state, idempotence or Kafka delivery behavior is needed for this arithmetic. Keep causal attribution unestablished. B10 accepted journaled probes with no terminal report retain unreported_seen/unreported_unseen and do not become phantom/loss because this observation was added.

### Wire choice and exact paths
Use one optional sibling object, duplicate_breakdown, with exactly the two required members duplicate_same_attempt and duplicate_different_attempt. Both are nonnegative JSON Int64 values. Never permit a half-object, explicit null object, null member, fraction, string, boolean, out-of-range value, duplicate member or unknown member. An absent object means not recorded/unavailable, never zero. The enclosing existing six-count object remains exactly six fields. Wherever an existing duplicate total accompanies this object, checked(same+different) must equal that duplicate total. A sum overflow or mismatch is malformed wire evidence, not a new scientific verdict threshold. This avoids widening every existing exact-six count schema and avoids a schema migration solely for optional observations.

Keep VerifierResult and PhaseCandidate schema_version=1, legacy B10 finalization schema_version=1, and scientific B10 schema_version=3/dialect=b10_abrupt. Updated readers accept historical omission at these versions and reject malformed presence. Old strict binaries will reject the new optional key; this is backward readability by the updated reader, not forward readability by old binaries. Ship writers and all consuming readers together. For record-based JsonSerializer paths, add property-specific ignore-when-null to the new nullable property: do not globally omit preexisting null fields, whose presence is part of the closed format.

For the following paths, append .duplicate_same_attempt and .duplicate_different_attempt to the object path to obtain the two exact leaf paths:
1. Ordinary verifier result: $.duplicate_breakdown, adjacent to existing root $.duplicate. Add nullable DuplicateBreakdown to VerifierResult; carry it from RunJudgementResult through VerifyRunHandler. Emit only where the existing path publishes complete_valid authoritative message counts. Invalid/incomplete results omit it along with authoritative counts. Ordinary B10 nonfinal VerifierResult continues to forbid authoritative counts and omits it.
2. Phase candidate: $.duplicate_breakdown beside $.counts; $.repeats[i].duplicate_breakdown beside $.repeats[i].counts. Carry nullable values through PhaseRepeatEvidence, PhaseRepeatResult, PhaseJudgement, PhaseCandidate, JudgeHandler and PhaseJudge. Repeat availability follows its existing accepted source/count gates. Root availability requires all five contributing valid repeat values present, each consistent with its duplicate count; checked sum the two components. A historical/mixed phase with any unrecorded repeat keeps existing six totals if otherwise authorized but omits root breakdown. Never substitute zero for the missing contribution.
3. Legacy B10 finalization: $.repeats[i].duplicate_breakdown beside $.repeats[i].counts in B10SweepEvidence/B10RepeatFinalization and B10SweepEvidenceCodec. Carry from the accepted full original+late record union through B10LateWriteChecks, B10SweepHandler, B10SweepChecks/PhaseJudge and candidate serialization. Retain repeated-coordinate invalidity and all ownership, immutable reference and finality checks.
4. Scientific B10 final sweep projection: $.data.repeats[i].duplicate_breakdown beside $.data.repeats[i].timeout; $.data.repeats[i].restart.arms[j].duplicate_breakdown. Add nullable values to B10FinalRepeat and B10FinalArm. Timeout availability follows existing complete_valid/classification_final/scan and successful timeout-join gates. Arm breakdown is cohort-local; it requires a complete scan and complete authenticated/reconciled identity for that arm's full record population, not successful classification of another cohort. Do not require a genuine terminal callback for an authorized unreported original. Timeout remains independent of restart eligibility; restart observations retain existing eligibility status.
5. Scientific B10 final candidate embeds the same B10FinalRepeat records: $.b10_final.repeats[i].duplicate_breakdown and $.b10_final.repeats[i].restart.arms[j].duplicate_breakdown. Its public repeats at $.repeats[i].duplicate_breakdown and its root $.duplicate_breakdown must equal the corresponding timeout projection values/sums when available. PhaseCandidateCodec.ValidateFinal must compare these new projections as well as its existing source/finality/count/rule comparisons. It must not put restart-arm counts into timeout/root message totals.
6. B10 abrupt existing nonfinal-result.json, scientific and diagnostic trial: $.data.duplicate_breakdown beside $.data.timeout; $.data.arms[j].duplicate_breakdown; $.data.arms[j].members[k].duplicate_breakdown. Carry timeout values from TimeoutEvidenceReader.Judge only after its existing per-instance validity and retry checks. Carry arm/member values from AbruptClassification and the scientific final evaluator's kill-too-late control branch, computed from full matched records before copy-list truncation. A member whose identity is opaque or malformed can retain existing copy/other diagnostics but has no breakdown; an arm with any such unresolved contributing member has no arm breakdown. Preserve full diagnostic population and existing pair/witness results. Update the scientific final input reader's exact allowed-field checks and original-window agreement to accept and validate present breakdowns; historical omission does not authorize zero. Agree the nonfinal observation against the original window, then recompute the final observation from owned original+late records rather than copying stale nonfinal values.

No aggregate across timeout, same_instance, cross_restart or warm-up is added. Warm-up stays excluded from timeout and probe counts. The two arm names describe scenario cohorts; they are independent of the two new attempt-identity categories: the same_instance arm can contain different-attempt duplicates, and multiple copies of one attempt in cross_restart belong to duplicate_same_attempt.

### B10 pair-count audit and constraints
AbruptClassification.cs computes each member pair_count=checked(original_copies*resend_copies), then sums pairs for the arm. For a member with three original copies and two resend copies, pair_count is 6 while excess duplicates are 4 and the decomposition is 3 same/1 different. Do not derive the new metrics from pair_count, originals_seen/resends_seen, a witness, the truncated copies array, or other_copies alone. All of those lose information needed by the formula. other_copies is the number matching logical key/sequence/payload whose headers do not identify the authorized original or resend; a malformed/null header can be retained as role other today. Preserve those diagnostics and make the new observation unavailable when they cannot be authenticated. Do not silently drop other copies and publish a partial zero. The final projection currently intentionally omits member/copy tables, so compute arm observations before ProjectArm; do not reintroduce those tables into final candidate JSON.

### Historical compatibility and publication
Updated codecs/readers can read historical artifacts with no breakdown; round-tripping omission keeps omission. Future evidence publication needs the breakdown wherever the current path authorizes the associated message counts; optional historical readability does not let new publications silently omit the new observation. Future B10 restart observations with opaque contributing copies retain the existing scientific status and expose a publication-completeness gap, not zero and not a new verdict result. Document this distinction in verifier-contract.md and b10-abrupt-contract.md. Do not add a new scientific validity gate or change timeout/restart rule selections. The generic phase.py transport currently checks candidate identity/status/repeat slots without copying count fields; verify via regression that the optional object passes intact and historical candidates still transport. Extend a publication validator only where that validator actually reads or copies the changed fields; do not edit frozen evidence/issue-492-b10-verdict/make_results.py, whose count keys and hashes deliberately describe its original inputs. Its current limitation is evidence that old publication code cannot backfill this new fact.

### Implementation placement
Introduce the observational value record in Common/Results and a pure shared classifier under VerifyRun/Judgement (within the existing VerifyRun slice). Keep OutcomeCounts's six fields unchanged. A separate nullable value in enclosing typed projections avoids changing equality/serialization of all existing OutcomeCounts instances and permits unknown history. Reuse the helper from ordinary OutcomeClassifier and abrupt/final cohort classifiers. No new project/package/interface, sidecar artifact, service or user option is needed. Keep the helper's validation policy subordinate to its existing caller's validity gates; missing identity is never assigned to a bucket. The ordinary classifier fixture that repeats the identical record three times must instead construct three records at distinct offsets while preserving its existing expected counts and assertions; add a separate reread test. This follows plan-pr Plan rules 2 and preserves its intended actual-extra-copy boundary.

### Enforced structural checks (not verdict thresholds)
D1. Optional duplicate_breakdown is all-or-none, exact two-field, nonnegative Int64 and budget-accounted in every supported reader/writer.
D2. Present components have checked sum equal to the associated existing duplicate total, wherever such a total exists. Arm/member totals are the checked sum of their components; they are not pair_count.
D3. Breakdown cannot accompany absent authoritative counts in ordinary/timeout projections, and cannot bypass existing finality/status/source acceptance. Arm diagnostics remain separately gated by complete authenticated cohort data.
D4. Candidate mirrored timeout observations and root sums agree with their accepted per-repeat source observations. Missing historical observation propagates absence, preserving otherwise authorized legacy count/status behavior.
D5. Checked arithmetic overflow never wraps/saturates to success. Existing failure/incomplete/diagnostic handling remains; reject malformed codec arithmetic, retain absent new observation where data is unrecorded, and never invent a zero. Since nonnegative components summing to an Int64 duplicate cannot themselves overflow if that duplicate total is valid, aggregate overflow tests must exercise real checked aggregation or codec malformed sum, not allocate impossible record lists.

### Literal test map
Use literal independent expected triples (duplicate,same,different), not expectations computed by the production helper: [3] -> (2,2,0); [1,1] -> (1,0,1); [3,2] -> (4,3,1). Here brackets are counts of distinct physical copies per full attempt identity for one logical group. Test empty -> (0,0,0), one copy -> (0,0,0), and one Kafka coordinate reread -> (0,0,0) where the caller permits rereads. Pin same bare attempt ID/different instances -> (1,0,1). Pin mixed logical groups as literal additive sums, key/sequence case sensitivity, same numeric offset across partitions, and same coordinate in overlapping original/late observations. Legacy B10 repeated-coordinate input remains invalid with counts/breakdown absent. Verify reordering remains independent and all four outcome counts unchanged.

For B10, pin separate timeout/same_instance/cross_restart/warm-up identities, earlier-repeat exclusion, late-only extra copies, original+late copies across partitions, permitted accepted/unreported originals without fabricated terminal outcomes, missing/malformed identity, opaque other copies, missing successor, scan incomplete and unknown loss. Test known empty eligible arm versus unknown arm and kill-too-late empty cross arm: only the known empty authorized population gets zero. Preserve the distinction that a failure in one cohort does not fabricate values for another. Keep copies above MaxDiagnosticsPerCheck to prove the new observation uses full records while the diagnostic list truncates. Pin [3,2] pair_count=6 alongside breakdown (3,1), and prove existing witness/comparison/rule selection unchanged.

At each codec location test absent historical object; present zero/nonzero; each missing member separately; empty object; null object/member; negative (-1), zero, one, Int64 maximum and overflow; fractions/booleans/strings/arrays; duplicate and unknown members; sum below/equal/above existing duplicate; checked overflow; status/finality/source mismatch; historical/new mixed phase aggregation; mirrored candidate mismatch; byte/entry budgets below/at/above measured serialized size. Keep unrelated evidence valid so each new predicate is reached. Assert writers and readers reject the same malformed presence. Keep strict rejection of unrelated unknown fields and old dialect/schema exclusions.

Keep new classifier arithmetic and serialization/aggregate tests in the mutation-only population; Component tests alone are insufficient because ParchwellVerifierMutationOnly removes Component/**/*.cs. Add focused component/host checks for final-result publication and reader propagation, then run the required gate order. Do not loosen/delete/skip existing assertions. For every fixture rejected by a new structural check: either repair fixture while retaining every assertion and exercised boundary, or retain intentionally invalid input and change expectations to the exact approved checks; list the fixture change and forcing check in PR notes.

### Mutation and acceptance route
Retain every current C5 target, especially PhaseJudge, B10LateWriteChecks, B10SweepHandler/B10SweepEvidenceCodec, B10FinalClassification/B10FinalProjection/B10FinalProjectionCodec/B10FinalRepeatEvidence/B10FinalJudge. Add the new DuplicateClassifier and every newly affected judgement/structural predicate-bearing file that is not already targeted (at least OutcomeClassifier, AbruptClassification, and the changed verifier/candidate codecs; include RunJudgement/TimeoutEvidenceReader if they acquire their own new predicate rather than passive forwarding). Keep config, c5_workflow.py TARGETS/selection/shard ownership and mirrored workflow matrices synchronized. Do not narrow mutation scope or exclude new tests for speed. Tests/Parchwell.Verifier.Tests.csproj is a read prerequisite for selection, not an authorization to remove tests.

Premerge qualification is a required preparation dependency: the changed targets, tests, build dependencies and layout must have a current source-bound, internally consistent qualification accepted by c5_runtime.py check for base/head. It cannot be deferred merely because hosted mutation acceptance runs later. The common work-bound table assigns finite diagnostic/profile/geometry bounds; do not borrow the old source-bound provisional/static-owner exceptions for this changed source. Use existing runtime limits and selection ownership, not an arbitrary postmerge escape. A failed/stale/missing/over-budget qualification blocks merge. Use the common work-bound table for the one 600-second mutation-only profile, changed target-set sizing passes and finding groups; preserve evidence and hand off before merge if it is insufficient. No campaign was run in planning.

After protected merge, control or named successor dispatches the existing mutation workflow on main only. Publication remains blocked until a passing applicable run measures a main commit containing all target changes, with summary measurement_complete:true and break_passed:true at the existing break threshold, complete retained shard evidence and no stale scope. A failed/incomplete applicable run follows the existing fix-issue rule and blocks evidence-pack publication; a linked follow-up alone is not completion. The approved hosted execution safeguards remain 16200 seconds per shard/300-minute job/no retry as documented; exceeding them records incomplete evidence, not acceptance. Performance qualification and mutation score are distinct. Verification gates are focused tests, cold review, cleared-source local sandbox gate, then final-head hosted .NET verification; docs checks and merge authority remain mandatory.

### Allowed change paths and corresponding read prerequisites
Core models and serializers:
- src/Parchwell.Verifier/Common/Results/VerifierResult.cs
- src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs
- src/Parchwell.Verifier/Common/Phase/B10SweepEvidence.cs
- src/Parchwell.Verifier/Common/Phase/B10SweepEvidenceCodec.cs
- src/Parchwell.Verifier/Common/Phase/B10FinalProjection.cs
- src/Parchwell.Verifier/Common/Phase/B10FinalProjectionCodec.cs
- src/Parchwell.Verifier/Judge/PhaseCandidate.cs
- src/Parchwell.Verifier/Judge/PhaseCandidateCodec.cs
Producers/readers/aggregation:
- src/Parchwell.Verifier/VerifyRun/Judgement/OutcomeClassifier.cs
- src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs
- src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs
- src/Parchwell.Verifier/Judge/PhaseJudge.cs
- src/Parchwell.Verifier/Judge/JudgeHandler.cs
- src/Parchwell.Verifier/VerifyRun/Judgement/B10LateWriteChecks.cs
- src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs
- src/Parchwell.Verifier/Judge/B10SweepChecks.cs
- src/Parchwell.Verifier/VerifyRun/Abrupt/TimeoutEvidenceReader.cs
- src/Parchwell.Verifier/VerifyRun/Abrupt/AbruptClassification.cs
- src/Parchwell.Verifier/VerifyRun/Abrupt/B10AbruptVerifyHandler.cs
- src/Parchwell.Verifier/VerifyRun/B10Final/B10FinalClassification.cs
- src/Parchwell.Verifier/VerifyRun/B10Final/B10FinalRepeatEvidence.cs
- src/Parchwell.Verifier/Judge/B10FinalJudge.cs
Tests/fixtures and allowed change paths:
- tests/Parchwell.Verifier.Tests/Judgement/OutcomeClassifierTests.cs
- tests/Parchwell.Verifier.Tests/Judgement/RunJudgementTests.cs
- tests/Parchwell.Verifier.Tests/Judgement/B10LateWriteChecksTests.cs
- tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs
- tests/Parchwell.Verifier.Tests/Judge/PhaseCandidateCodecTests.cs
- tests/Parchwell.Verifier.Tests/Judge/PhaseJudgeTests.cs
- tests/Parchwell.Verifier.Tests/Judge/B10FinalProjectionCodecTests.cs
- tests/Parchwell.Verifier.Tests/Judge/B10FinalJudgeTests.cs
- tests/Parchwell.Verifier.Tests/Judge/B10SweepChecksTests.cs
- tests/Parchwell.Verifier.Tests/Component/VerifierOutcomeComponentTests.cs
- tests/Parchwell.Verifier.Tests/Component/B10AbruptVerifyClassificationTests.cs
- tests/Parchwell.Verifier.Tests/Component/B10FinalEvidenceContractTests.cs
- tests/Parchwell.Verifier.Tests/Component/B10FinalJudgeIntegrationTests.cs
- tests/Parchwell.Verifier.Tests/Component/B10FinalSweepHandlerTests.cs
- tests/Parchwell.Verifier.Tests/Support/B10AbruptVerifyEvidence.cs
- tests/Parchwell.Verifier.Tests/Support/B10AbruptVerifyFixtureBuilder.cs
- tests/Parchwell.Verifier.Tests/Support/B10SweepFixture.cs
- tests/Parchwell.Verifier.Tests/Judge/B10FinalJudgeTests.cs (nested B10FinalJudgeFixture, no separate fixture file)
- tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj (selection authority, no planned exclusion change)
- new Common/Results/DuplicateBreakdown.cs, VerifyRun/Judgement/DuplicateClassifier.cs and tests for their behavior.
Mutation/runtime/contracts/consumers:
- tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json
- tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py
- tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py
- tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json
- tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py
- tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py
- .github/workflows/verifier-c5-mutation.yml (update a committed mirror only if one exists at implementation time; no such mirror exists on this plan base)
- docs/operations/a01-runtime-limits.json (read; preserve limits)
- docs/operations/verifier-mutation.md
- docs/operations/verifier-contract.md
- docs/operations/b10-abrupt-contract.md
- infra/scripts/phase/phase.py and infra/scripts/phase/tests/test_phase.py (transport compatibility; change only as necessary to prove pass-through/publication contract)
- infra/scripts/phase/b10_abrupt.py and its test_b10_abrupt.py/test_b10_scientific.py (read applicable nonfinal consumers; no new scientific judgement)
- docs/operations/evidence/issue-492-b10-verdict/make_results.py (read-only historical publication limitation; do not edit)
- docs/blueprint.md ADR-012/013/019/020/021, docs/engineering/dotnet.md Testing, docs/agents/factory.md Outcome responsibility/Authority reads/Item lifecycle/Evidence pack and AGENTS.md Truth and verification/Skills.

## Acceptance commands and completion evidence

Planning/checking is read-only. The commands below are future implementation acceptance, not claims that this planning session ran them.

| Stage | Commands/evidence |
| --- | --- |
| PR1 phase/transfer contracts | `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; each named archive negative/positive fixture passes without Azure credentials |
| PR1 persistent configuration | `terraform fmt -check -recursive infra`; `terraform -chdir=infra/persistent init -backend=false -input=false`; `terraform -chdir=infra/persistent validate`; `terraform -chdir=infra/persistent test`; existing tflint gate. Mocked tests establish inputs, not live Azure behavior |
| PR1 capture behavior | `./scripts/verify-dotnet.sh --tier fast --project Parchwell.Verifier.Tests`; focused component/host tests for common consume capture; later required sandbox/full gates |
| PR2 | `./scripts/verify-dotnet.sh --tier fast --project Parchwell.Kafka.Tests --project Parchwell.LoadGen.Tests --project Parchwell.Verifier.Tests --project Parchwell.Enrichment.Tests`; exact catalogue coverage, counter/state/absence cases, every existing callback path and resend transition tests |
| PR3 | `./scripts/verify-dotnet.sh --tier fast --project Parchwell.Verifier.Tests`; literal arithmetic/codec/cohort matrix and focused component propagation; source-bound mutation preparation as below |
| All implementation PRs | `npm run check:docs`, `git diff --check`; focused checks before cold review, then repository-required cleared-source sandbox gate and final-head hosted checks under pull-requests.md Verification gates. This table does not replace their order or permit a fast-only merge |
| C5 premerge preparation where affected | `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py profile --output-dir DIR --timeout 600`; current qualification/selection preparation under `docs/operations/verifier-mutation.md`; `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base BASE_SHA --head HEAD_SHA --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` |
| C5 postmerge | Existing `verifier-c5-mutation.yml` dispatched only on `main`; complete applicable evidence with `measurement_complete: true`, `break_passed: true`, existing break threshold and complete scope, before future evidence-pack publication |
| Persistent archive smoke | Hari's persistent plan/apply, sanitized output configuration, an authorized existing-network workflow on `main`, fresh-connection read/create/readback/conflict/cleanup evidence. Record idle versus under-load and exact volume; no smoke or Azure dispatch is authorized by this document's publication |

`DIR`, `BASE_SHA` and `HEAD_SHA` above are runtime placeholders, not shell variables silently defaulting to repository paths. Keep program output outside Git. New native-metric/exporter replay and archive/codec fixtures are meaningful offline checks; they do not claim Azure behavior.

## Deployment prerequisites and route to approval

No archive live smoke has been supplied to this planning session. [factory.md Item lifecycle step 1](../../docs/agents/factory.md#item-lifecycle) states: "A plan that changes live infrastructure is not approvable until a live smoke run of that design exists. A smoke-only pull request may precede plan approval for this purpose."

Accordingly, the PR1 section is a concrete archive design and bounded smoke prerequisite, not a claim that the whole set can already be approved. A directly authorized smoke-only prerequisite may implement the necessary persistent configuration/workflow through that existing exception. Hari merges protected work, plans/applies the persistent layer and supplies its outputs. A successor obtains the bounded main-only smoke evidence, then returns it for plan approval. This is readiness for the agreed PR1 archive capability, not permission to fold metrics or duplicate judgement into a smoke change. If an additional prerequisite PR is necessary, record it explicitly and obtain the split amendment rather than silently treating it as one of the three product PRs.

Other unverified deployment assumptions have acceptance work, not assumed passes: runner private DNS and routing to both storage accounts; actual source/destination container grants; pinned CLI child-file-cap/conditional-transfer behavior; configured transfer volume within 600 s; finite metadata capture under interruption; new consumer callback/telemetry provider cleanup; export reception and backend dimension settings/limits. Test each fresh connection and state idle/under load in evidence. An unresolved material assumption remains a blocking plan-check finding unless Hari accepts the named risk under factory.md; do not ask Terra to invent that acceptance.

## Deferred work

| Work | Reason, successor, bound and blocked gate |
| --- | --- |
| Persistent apply and live archive smoke | Hari-only persistent execution plus authorized workflow successor after protected merge; one bounded smoke dispatch from the table above. Offline preparation is completed first. Missing smoke blocks archive plan approval under the quoted rule; missing successful completion blocks feature activation/publication |
| Applicable hosted mutation measurement | Authorized only on `main` after target changes merge. Implementation completes source/test/qualification preparation before merge; control/successor runs one configured campaign under existing execution bounds. Failure/incompleteness keeps future evidence-pack publication blocked |
| Backend billing/ingestion confirmation | Requires approved paid run; preceding offline registry/replay/price model completed in PR2. One approved smoke observation within that run; record quota/drop/ingestion evidence, not a guessed cost. Missing reception/limit evidence blocks claiming end-to-end telemetry readiness |
| Dashboard and alert deployments | User asked to preserve later testing capability, not to deploy now. A later separately approved task chooses panels, thresholds and paid resources. No gate on this issue's offline implementation; do not downgrade data paths so this later use becomes impossible |
| Agent read-only Azure identities | Entirely in #496; owner/successor there handles service principals, secrets, policy and private-network access. Not an implicit prerequisite or deliverable here; this archive uses existing workflow access and operator retrieval |

## Plan-check questions

1. Runtime-sized work is bounded in the resource/work table and each PR's enforced checks. Required source/authority reads are exempt under AGENTS.md rule 11. Runtime populations/finding dispositions that still exceed their stated preparation bounds hand off before merge; do not convert a missing premerge qualification into a postmerge task.
2. This plan adds required retained evidence where ADR-020 previously removed it, and adds explicit archive/publication completion and structural observation checks. Those changes come from Hari's grilling decisions. It adds no stricter Kafka scientific threshold or hidden parent-plan requirement. New monitoring validation suppresses the new projection without widening existing scientific validity checks.
3. Producer/consumer sources, schema owners and pinned native sources exist on the base. Persistent archive resources, transfer flags/runtime enforcement and backend delivery are not yet deployed/proven. PR1's preparatory smoke and PR2's offline replay/live reception evidence settle these dependencies; the deployment section states the remaining blockers. No later unmerged feature supplies the current PR's acceptance inputs.

## Sources and verification method

Use `gh api` REST and current `main` source reads for repository facts. Official external sources were read through the web documentation tool and direct HTTPS fetches of pinned upstream sources and public retail-pricing responses. No Azure token, Azure resource call, build, live test or workflow dispatch was used in writing this plan.

- [librdkafka v2.16.0 statistics](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md), [native emitter](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c), native state/request sources cited in PR2.
- [Application Insights aggregation/pricing](https://learn.microsoft.com/en-us/azure/azure-monitor/app/metrics-overview), [AppMetrics eligibility](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/appmetrics), [AppTraces eligibility](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/apptraces), [table-plan comparison](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/data-platform-logs#table-plans), [current Basic/Auxiliary queries](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/basic-logs-query#query-capabilities).
- [Blob access tiers](https://learn.microsoft.com/en-us/azure/storage/blobs/access-tiers-overview), [lifecycle behavior](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview), [private endpoint/copy considerations](https://learn.microsoft.com/en-us/azure/storage/common/storage-private-endpoints).
- [AVM storage 0.10.0 pinned source](https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/tree/5695e5ec21744a3765051677d9e82bfba16b5d98).
- [Log Analytics UK South GBP retail query](https://prices.azure.com/api/retail/prices?currencyCode=GBP&$filter=armRegionName%20eq%20%27uksouth%27%20and%20serviceName%20eq%20%27Log%20Analytics%27%20and%20priceType%20eq%20%27Consumption%27), [Storage UK South GBP](https://prices.azure.com/api/retail/prices?currencyCode=GBP&$filter=armRegionName%20eq%20%27uksouth%27%20and%20serviceName%20eq%20%27Storage%27%20and%20productName%20eq%20%27General%20Block%20Blob%20v2%27%20and%20priceType%20eq%20%27Consumption%27), [Private Link GBP global meters](https://prices.azure.com/api/retail/prices?currencyCode=GBP&$filter=productName%20eq%20%27Virtual%20Network%20Private%20Link%27%20and%20armRegionName%20eq%20%27Global%27%20and%20priceType%20eq%20%27Consumption%27).
- [GitHub artifact retention](https://docs.github.com/en/actions/tutorials/store-and-share-data#configuring-a-custom-artifact-retention-period), [Enterprise artifact billing terms](https://docs.github.com/en/enterprise-cloud@latest/billing/concepts/product-billing/github-actions).

## Pinned Confluent API source additions

[Timestamp](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Timestamp.cs), [TimestampType](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/TimestampType.cs), [Headers](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Headers.cs), and [Header](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Header.cs) were read at v2.16.0 for raw timestamp, header enumeration and nullable byte-value behavior. Metadata preserves these observations before existing decoding.

## Open questions for Hari

Design choices from grilling are settled. Approval readiness is not: the archive live smoke required by factory.md does not exist in the evidence read for this plan. Hari must authorize the bounded smoke-only prerequisite route or provide existing applicable smoke evidence before archive plan approval. New Azure costs and the persistent apply are not authorized here. Do not record `None` until that gate is resolved or the named deployment risks have the approval the governing process requires.

## Stop rule and handoff prompt

Stop and hand off under AGENTS.md GOVERNANCE Handoffs if the selected PR needs a contract, requirement, scope, policy value or scientific rule different from this plan, or if a stated gate/bound cannot be satisfied. Preserve completed work and the concrete unresolved action. A new issue, skipped assertion or warning does not discharge a required gate.

Paste this after selecting **GPT-6.1 Terra** and **high** effort:

```text
Use the implement-plan skill on issue 494. Read the current approved
observability-evidence plan file at the exact commit named by its plan comment,
the corresponding plan check and the recognized later decisions. Implement only
the earliest eligible unmerged PR in its ordered set. Keep agent Azure identities
and credential setup in issue 496. Use the plan's native registry, exact wire
contracts, literal test matrix and bounds; leave private method/file organization
to engineering judgment. Verify current main against the plan base before edits.
Do not infer plan approval from this prompt or a preexisting ready-for-agent label.
If the archive smoke/approval prerequisite is unresolved, report that precise gate
and the bounded prerequisite action; do not improvise Azure execution or a waiver.
Complete all authorized preparation and required review/checks. Hand off with
evidence at the earliest repository-authorized checkpoint.
```
