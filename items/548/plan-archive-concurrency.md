# archive-concurrency contract-grade plan

Revision: 1

This plan makes a C01, C03 or C10 phase archive fit inside its fixed 600-second deadline by moving blob bytes through the Blob SDK instead of one Azure CLI process per call, and then collecting the five repeats concurrently. Running the existing CLI calls in parallel would gain nothing: the CLI session wrapper holds one exclusive lock for the whole of every `az` command. The SDK route changes a transport rule of the approved observability specification, so it needs Hari's decision (open question 1) before either pull request is approvable.

Two pull requests, in order, under factory.md "Item lifecycle" step 3 "Split by scope". Base: `main` at `08c708aae6a8cc27c87110013540089bb5553a43`. Line numbers are at that base.

| Order | Pull request | Piece |
|---|---|---|
| 1 | `548-sdk-transport` | Replace the CLI storage-call transport with an SDK helper behind the existing `Transfer.command` seam. Every phase archive uses it. |
| 2 | `548-consumer-workers` | Collect the five repeats of a consumer phase in five workers, each with its own spool and helper. Predecessor: `548-sdk-transport` merged. |

## 1. Goal and authority

Goal: a consumer phase whose evidence is all present archives every object before `ArchiveDeadlineSeconds` (600 s) expires, so its receipt carries no `deadline` gap. The deadline stays 600 s. The time the archive needs is measured at the next C03 smoke run, not estimated into a new deadline.

Authority, in creation order:
- Issue 548 body (`updated_at` 2026-11-18T08:25:04Z). It records the C03 smoke run [44248514485](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44248514485): the C1 step "Archive phase evidence" ran 08:02:07 to 08:12:07 UTC and both phases hit the deadline ([smoke report 6973117302](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6973117302), check 8). It records Hari's choice, in the #347 control session on 2026-11-18, to make the archive transfer objects concurrently rather than raise the deadline, and that this gates the #508 batch dispatch.
- [Decision 6943512296 on issue 531](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943512296), point 2, cited by the smoke report: the overrun risk was accepted by name and Hari decides the response before the C03 batch. Issue 548 is that response.
- The approved observability specification, [items/494/plan-observability-evidence.md at f96eab914d00c06484431ff67a1808fb331c7e13](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md), section "Resource and work bounds". Its transport paragraph (line 212) says: "use the existing serialized Azure CLI subprocess as transport ... No shell, no new package, token extraction or credential persistence." Its preflight paragraph (line 215) says: "do not alter the general session wrapper's authentication/renewal behavior." Pull request 1 departs from line 212. Open question 1 asks Hari to amend it.
- `docs/operations/archive-smoke.md` line 68: the archive smoke's SDK observation already "obtains a token through the existing serialized CLI session and passes the token and its actual expiry to the CLI's Python interpreter through standard input." Pull request 1 reuses that mechanism for data transfer.
- [Decision 6973159782](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973159782): Claude Code implements this issue, on the implementation model; Codex reviews; the plan needs Hari's approval and `ready-for-agent`.
- [Decision 6973214094](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973214094): point 1, the plan evaluates server-side copy alongside concurrent transfers through the runner, citing current Microsoft documentation for each claim about copy behaviour (section 1.2); point 3, the workflow identity does the copy and workloads get no archive access (`docs/blueprint.md` line 652). Its point 2 is replaced by the next decision.
- [Decision 6973250625](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973250625), reworded point 2: copies may be asynchronous, concurrent or server-side, in any order, but every archived object is verified: the source etag read is bound to the copy, the destination content hash matches the source, and each copy is confirmed complete. All copies complete and are verified before the ephemeral source storage is destroyed; an object not verified in time is an archive gap, which holds publication (`docs/blueprint.md` line 651). Section 5 maps each point to the design.
- Control session for issue 347, message of 2026-11-18 to this session: issue 549 investigates the missing native-monitoring indexes and may touch the same archive code; keep this scope to transfer concurrency. Evidence of coordination, not a decision.

### 1.1 Measured evidence

Read by script from the archive artifacts of run 44248514485 (`c03-C0-archive-44248514485-1`, `c03-C1-archive-44248514485-1`), 2026-11-18:

| Phase | Expected objects | Verified | Failed `deadline` | Of which `native_monitoring_index` | Verified bytes |
|---|---|---|---|---|---|
| C0 | 202 | 141 | 61 | 25 | 3547510 |
| C1 | 202 | 128 | 74 | 25 | 2818991 |

The largest verified object is 388865 bytes. The time goes on calls, not bytes.

Source read on `main`:
- `infra/scripts/phase/archive.py` lines 73-77: every storage call is one `az storage blob` subprocess through `self.command`, which defaults to `_common.command` (`infra/scripts/archive/smoke.py` lines 189-256).
- `infra/scripts/phase/archive.py` lines 266-311 (`Transfer.copy`): a fresh object costs six calls (source `show`, source `download`, destination `show`, `upload`, destination `show`, destination `download`); `test_archive_transfer.py` line 49 pins that sequence.
- `scripts/azure-cli-session/az` lines 406-438: the wrapper takes `fcntl.flock(..., LOCK_EX)` on one session lock file and holds it until the real CLI command exits. Two `az` invocations never overlap. `.github/workflows/scenario.yml` lines 146-149 put this wrapper first on `PATH` for the archive step.
- `infra/scripts/phase/archive.py` line 2178: the archive already runs `_common.preflight`, which resolves the CLI's own Python and requires `azure-storage-blob` `12.29.0b1` in it (`smoke.py` lines 259-277).

Estimate (2026-11-18, basis: the table above, assuming time per object is constant across a phase): at the C0 rate of 141 objects in at most 600 s, 177 objects need about 750 s and 202 objects about 860 s, before any native-monitoring raw chunks that issue 549 may add. Not measured.

### 1.2 Server-side copy evaluated (decision 6973214094 point 1)

Server-side copy means the archive account fetches the source blob itself (`Copy Blob From URL`, synchronous, or `Copy Blob`, asynchronous), so the bytes do not pass through the runner. Microsoft Learn, read 2026-11-18 with the Microsoft Learn MCP server:
- `Copy Blob From URL` copies synchronously "for source blob sizes up to 256 mebibytes (MiB)", with `x-ms-source-if-match` to bind the source ETag (412 on mismatch) and `If-None-Match: *` for create-only (https://learn.microsoft.com/rest/api/storageservices/copy-blob-from-url, "Request"). That would satisfy the etag binding and create-only rules.
- The same page is inconsistent on source authorization. Its "Authorization" table says a source block blob in another storage account supports Microsoft Entra ID authorization: "No", only SAS. Its "Remarks" say "In version 2020-10-02 and later, Microsoft Entra authorization is supported for the source of the copy operation", through `x-ms-copy-source-authorization` with scheme bearer. Which holds for this account pair is not verified. A SAS is not an option here: both accounts set `shared_access_key_enabled = false` (`infra/ephemeral/storage.tf` line 12, `infra/persistent/archive.tf` line 12), and archive-smoke.md line 91 accepts no SAS URL.
- Both accounts set `public_network_access_enabled = false` (`infra/ephemeral/storage.tf` line 11, `infra/persistent/archive.tf` line 11) and are reached through private endpoints. Microsoft documents that copy between accounts with network restrictions depends on topology, and that the destination service fetching from a firewalled source can fail with `403 ... CannotVerifyCopySource` (https://learn.microsoft.com/troubleshoot/azure/azure-storage/blobs/connectivity/copy-blobs-between-storage-accounts-network-restriction). Whether the archive account can read the ephemeral source in this network is not verified and would need a live smoke (factory.md "Item lifecycle" step 1: a plan that changes live infrastructure needs a smoke first, and enabling it might need network or `AllowedCopyScope` changes on protected Terraform).
- Server-side copy does not remove the source read. Decision 6973250625 requires the destination hash to match the source, and the source's SHA-256 is known only by reading it: blob properties carry `Content-MD5` at most, and the approved specification line 211 says "Metadata hash or Content-MD5 alone is not SHA256 content verification". Many sources are also inspected before archiving (`inspect_source` callbacks in `Collector.source`, consumer completion and registration checks, projections). So each object would still need a source download and a destination readback; server-side copy replaces only the upload.

Conclusion: server-side copy saves at most one of six calls per object, adds two unverified prerequisites (Entra source authorization across accounts, and network reachability between private accounts), and may need protected infrastructure changes. This plan does not choose it. Concurrent transfers through the runner keep every existing verification step and need no infrastructure change. Open question 1 lists server-side copy as alternative (d).

## 2. Scope

### 2.1 Pull request 1, `548-sdk-transport`

Files allowed to change:
- `infra/scripts/phase/archive_sdk.py` (new): the parent-side command adapter and token holder.
- `infra/scripts/phase/archive_sdk_helper.py` (new): the helper process run by the CLI's Python. The implementer may merge it into `archive_sdk.py` if one file can serve both roles without importing the SDK in the parent.
- `infra/scripts/phase/archive.py`: `execute` only (lines 2154-2271), to construct `Transfer` with the new command and to stop helpers at exit.
- `infra/scripts/phase/tests/test_archive_sdk.py` (new).
- `docs/operations/archive-smoke.md`: the two sentences in section 9.

### 2.2 Pull request 2, `548-consumer-workers`

Files allowed to change:
- `infra/scripts/phase/archive.py`: `Collector` (lines 940-1595), `read_job_pods` (lines 1621-1720) only where it reads `collector.transfer.spool`, `collect_ordinary` (lines 1856-1981) and `execute`.
- `infra/scripts/phase/archive_sdk.py`, only if a worker needs a factory it lacks.
- `infra/scripts/phase/tests/test_archive_workers.py` (new).
- `docs/operations/archive-smoke.md`: the paragraph in section 9.

### 2.3 Paths that must not change (both pull requests)

- `scripts/azure-cli-session/az` and its tests. Its lock and renewal stay as they are (specification line 215).
- `infra/scripts/archive/smoke.py` and the archive smoke workflow. The smoke keeps the CLI transport.
- `.github/workflows/**` and `docs/operations/workflows/**`. No workflow change is needed: the archive step already runs with the wrapper on `PATH` and `_common.preflight` already finds the CLI's Python.
- `collect_native_monitoring` (lines 1984-2073), `archive_native_monitoring.py`, `archive_nonledger.py`: issue 549's area. They keep their sequential behaviour.
- `GAP_CODES`, `validate_gaps`, the manifest, marker and receipt schemas, `_common.POLICY` and every constant in `archive.py` lines 5-11.
- Every existing test assertion in `infra/scripts/phase/tests/` and `infra/scripts/tests/`.

## 3. External contracts

Unchanged and relied on:
- Manifest, completion marker and receipt: field sets, `policy` object (including `deadline_seconds: 600` and `transfer_passes: 1`), sort orders in `execute` lines 2242-2244 and the 100-gap cap with `diagnostics_truncated` (`Collector.gap`, lines 955-961).
- Gap codes and the boundary strings that `Transfer` already raises: `properties`, `invalid properties`, `conditional download`, `conditional create`, `object properties`, `command timeout`, `global deadline`, `command interrupted`, `command output limit`, `spool reserve`.
- Destination layout `v1/<commit>/<run id>/<attempt>/<run>/<phase>/...`, container `run-evidence`, create-only uploads, ETag-conditional downloads, independent length and SHA-256 readback (archive-smoke.md line 101).
- The `Transfer` command seam: `command(args, deadline)` returns `(returncode, stdout bytes, stderr bytes)` or raises `Gap` (`archive.py` lines 73-77).

Changed:
- Transport (pull request 1). Storage calls of a phase archive run as SDK requests in a helper process started with the CLI's Python, authenticated by an access token for `https://storage.azure.com/` obtained with `az account get-access-token` through the serialized wrapper. New boundary strings, both under the existing code `transport`: `storage token` and `sdk helper`.
- Concurrency (pull request 2). For C01, C03 and C10 only, collection of repeats 1 to 5 runs in five concurrent workers. The spool reserve check on that path requires 2689925120 free bytes (5 x 537985024).
- `docs/operations/archive-smoke.md` wording in section 9.

## 4. Requirements

### 4.1 Pull request 1

R1. WHEN `execute` builds the phase `Transfer`, THEN it passes an SDK command adapter as `command`, bound to the CLI Python that `_common.preflight` returned. `Transfer` and `Collector` code is unchanged.

R2. WHEN the adapter receives argv `['az', 'storage', 'blob', <action>, '--account-name', A, '--container-name', C, '--name', N, '--auth-mode', 'login', '--only-show-errors', *extra]` with `<action>` and `extra` exactly as `Transfer.properties`, `Transfer.download` or `Transfer.upload` produce them (`archive.py` lines 79, 104-106, 113-115), THEN it performs one helper request: `show` is Get Blob Properties; `download` is Get Blob with `If-Match: <etag>` into `--file`, one connection; `upload` is Put Blob to a block blob with `If-None-Match: *`, one connection, from `--file`. WHEN argv has any other shape, THEN it returns `(2, b'', b'')` and sends nothing.

R3. WHEN a `show` succeeds, THEN stdout is the JSON `{"properties":{"contentLength":<int>,"etag":"<etag>"}}` and the return code 0. `download` and `upload` success return `(0, b'', b'')`.

R4. WHEN a request fails with a storage service error, THEN the return code is 1 and stderr is the service error code as ASCII (for example `b'BlobNotFound'`, `b'ConditionNotMet'`, `b'BlobAlreadyExists'`). WHEN the SDK raises `ResourceModifiedError` on a conditional download, THEN stderr is `b'ResourceModifiedError'`. Any other failure returns 1 with empty stderr. The existing `Transfer` regexes then map these to `source_missing`, `source_changed`, `destination_conflict` or `transport` exactly as for the CLI.

R5. WHEN a request has no answer within `min(60 s, deadline - now)`, THEN the adapter stops the helper process with `_common.stop_command`, raises `Gap('deadline', 'command timeout')`, and does not resend that request. The next request starts a new helper. WHEN `deadline - now <= 0` before a request, THEN it raises `Gap('deadline', 'global deadline')` and sends nothing.

R6. WHEN the adapter needs a token (first request, or the held token's `expires_on` is less than 120 s after now), THEN it runs `_common.command(['az', 'account', 'get-access-token', '--resource', 'https://storage.azure.com/', '--output', 'json', '--only-show-errors'], min(deadline, now + 15), cap=16384)` once, and validates `accessToken` against `[A-Za-z0-9._-]{1,8192}` and `expires_on` as an integer in `1..4102444800`, as `smoke.py` lines 413-424 do. WHEN that fails or is invalid, THEN the current request raises `Gap('transport', 'storage token')`. The next request may try once more.

R7. WHEN the helper starts, THEN it is started with the CLI Python, `start_new_session=True` and `_common.limit_child` as `preexec_fn`, and receives the account hosts, token and expiry only on standard input. It builds one `BlobServiceClient` per storage account with the SDK's default retry policy and `max_single_get_size` and `max_single_put_size` left at their defaults, and serves requests one at a time.

R8. WHEN the helper cannot start, exits, or writes a response that is not one JSON object with exactly the fields the adapter expects, THEN the request returns 1 with empty stderr for `download` and `upload`, and for `show` the adapter raises `Gap('transport', 'sdk helper')`.

R9. WHEN `execute` returns or raises, including after `KeyboardInterrupt`, THEN every helper process it started has been stopped and waited for.

R10. The token never appears in any process argv, environment variable, file, log line, stdout or stderr of the parent, or in any file under the spool or public directories.

### 4.2 Pull request 2

R11. WHEN `phase['scenario']` is C01, C03 or C10, THEN `collect_ordinary` processes repeats 1 to 5 in five concurrent workers. Each worker handles that repeat's planned modes in the existing planned order. After all five finish, the main thread handles the phase-level modes (`judge`) in order. Units are returned in the same order as the sequential code would return them.

R12. WHEN any other scenario runs, THEN collection is sequential, exactly as on `main`.

R13. WHEN a worker runs, THEN it has its own `Transfer` with its own spool directory `<spool>/w<repeat>` (mode 0700) and its own SDK command adapter. No two concurrent operations use the same spool path. The main thread keeps the original `Transfer` for phase-level modes, native monitoring and controls.

R14. WHEN the concurrent path checks the spool reserve, THEN it requires free space of at least 2689925120 bytes; below that it raises `Gap('disk_space', 'spool reserve')`. The sequential path keeps 537985024.

R15. WHEN workers run, THEN every change to shared `Collector` state (`objects`, `captures`, `gaps`, `names`, `window_sources`, `native_existing`, `verified_bytes`, `logical_bytes` and the object-count check) happens under one lock, and the lock is not held during a storage call.

R16. WHEN the main thread receives `KeyboardInterrupt` while workers run, THEN it stops every worker's helper, waits at most 10 s for the workers, and `execute` records `Gap('interrupted', 'inventory interrupted')` through its existing path (lines 2235-2236). No completion marker is written.

R17. WHEN all workers finish, THEN `manifest['objects']`, `manifest['captures']`, `manifest['gaps']`, `manifest['expected_units']` and `manifest['totals']` equal those of a sequential run over the same inputs, whenever fewer than 100 gaps occur. With 100 or more gaps the stored gaps still number exactly 100, the last being `diagnostics_truncated`; which other gaps survive may differ.

## 5. Invariants and safety rules

- At most six storage calls per object and no helper-level retry (specification line 210). A timed-out request is never resent.
- One absolute 600-second monotonic deadline per phase archive, shared by all workers and helpers.
- Create-only uploads, ETag-conditional downloads, independent readback before `verified`. A destination conflict is never overwritten.
- At most 256 MiB written per child file (`RLIMIT_FSIZE` through `limit_child`).
- No new package: the helper uses the `azure-storage-blob` `12.29.0b1` already pinned and checked in the CLI's Python. No new Azure identity, role, secret or environment variable (hard safety rules 3 and 7). The token lives only in process memory and pipes of the workflow job.
- The general session wrapper is not changed.
- No result, verdict, expected outcome or file under `results/` changes.
- Decision 6973250625, point by point. Source etag bound: every source download is `If-Match` on the etag from the properties call, and a mismatch is `source_changed` (`Transfer.download`, lines 103-109). Destination hash matches source: `verified` only after the destination readback's SHA-256 equals the source's (`Transfer.copy`, lines 298-301). Copy confirmed complete: the readback is the confirmation; nothing is assumed from the upload response. Before teardown: the archive step (`scenario.yml` line 237) runs before "Destroy ephemeral infrastructure" (line 261), and the shared deadline ends all work inside that step; an object not verified by then keeps its gap. Concurrency does not change any of these per-object steps.
- Decision 6973214094 point 3: the helper runs in the workflow job with the workflow identity's token; workloads gain no archive access.

## 6. Test map

Fixture: a fake helper written for the test, run with `sys.executable`, that serves the adapter's protocol from an in-memory blob table and records each request. The SDK-calling helper itself is tested against a fake `azure` package placed on its `sys.path`, which records constructor and method arguments. The real SDK is not installed in CI.

Standing fixture rule (plan rule 2, modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2): when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) change the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keep the input and change the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it; the cold review and Codex review check each listed change. Pull request 1 is not expected to trigger it, because `Transfer` and its fake `command` tests are unchanged.

### 6.1 Pull request 1, `test_archive_sdk.py`

| Test | Requirement | Input | Expected |
|---|---|---|---|
| `test_copy_through_adapter_fresh_destination` | R1-R3 | source `('source','ledger','run/a')` = `b'abc'`, no destination; `Transfer('source','dest',spool,deadline,adapter).copy('ledger','run/a','v1/a',3)` | returns `(3, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', <source etag>, <dest etag>)`; helper ops `['show','download','show','upload','show','download']`; spool empty after |
| `test_copy_existing_destination` | R2 | as above, destination already `b'abc'` | ops `['show','download','show','download']`; same tuple |
| `test_rejects_unknown_argv` | R2 | `['az','storage','blob','delete',...]`; `upload` without `--if-none-match` | `(2, b'', b'')`; zero helper ops |
| `test_show_output_shape` | R3 | `show` of a 3-byte blob, etag `"0x1"` | stdout parses to exactly `{'properties': {'contentLength': 3, 'etag': '"0x1"'}}` |
| `test_service_codes_map` | R4 | helper answers `BlobNotFound` on show; `ConditionNotMet` on source download; `BlobAlreadyExists` on upload | `properties(..., missing_ok=True)` returns `None`; `copy` raises `Gap('source_changed','conditional download')`; `copy` raises `Gap('destination_conflict','conditional create')` |
| `test_request_timeout_not_resent` | R5 | fake clock; helper never answers the source download | `Gap('deadline','command timeout')`; helper stopped; that object's ops `['show','download']`; next copy starts a second helper (start count 2) |
| `test_global_deadline` | R5 | deadline already past | `Gap('deadline','global deadline')`; zero helper ops |
| `test_token_once_and_refresh` | R6 | token `expires_on` = now + 3600; then a second token at now + 100 | one `get-access-token` call for 10 copies; with expiry now + 100, the next request makes a second call |
| `test_token_failure` | R6 | `get-access-token` returns 1 | `Gap('transport','storage token')` on the first `properties`; zero helper ops |
| `test_helper_invalid_response` | R8 | helper answers `{"ok":true,"extra":1}` | `show`: `Gap('transport','sdk helper')`; `download`: `Transfer` raises `Gap('transport','conditional download')` |
| `test_helper_started_bounded` | R7 | patch `subprocess.Popen` | args `[<cli python>, <helper path>]`; `start_new_session=True`; `preexec_fn is _common.limit_child`; token absent from args and `env` |
| `test_helper_sdk_calls` | R2, R7 | fake `azure` package; helper served one `show`, one `download` with etag `"0x1"`, one `upload` | `get_blob_properties()`; `download_blob(etag='"0x1"', match_condition=MatchConditions.IfNotModified, max_concurrency=1)`; `upload_blob(<stream>, overwrite=False, max_concurrency=1)`; two `BlobServiceClient` constructions, one per account URL |
| `test_execute_stops_helpers` | R9 | `execute` with a fake inventory that raises `KeyboardInterrupt` after one copy | every started helper has exited; receipt `archive_complete: false` with an `interrupted` gap |
| `test_token_never_leaks` | R10 | token `SENTINELTOKEN0123456789` | absent from every Popen argv and env, from captured stdout and stderr, and from every file under the spool and public directories after `execute` |

Existing `test_archive_transfer.py`, `test_archive_boundaries.py` and the archive smoke tests run unchanged.

### 6.2 Pull request 2, `test_archive_workers.py`

| Test | Requirement | Input | Expected |
|---|---|---|---|
| `test_consumer_repeats_run_concurrently` | R11 | C03 phase status fixture from `test_archive_consumer.py`; fake transfers whose first `copy` waits on `threading.Barrier(5, timeout=5)` | barrier passes (5 concurrent workers); returned units equal the sequential order |
| `test_concurrent_equals_sequential` | R17 | same fixture, all objects present; run once with workers and once forced sequential | `objects`, `captures`, `gaps`, `expected_units`, `totals` equal |
| `test_non_consumer_stays_sequential` | R12 | A01-shaped fixture (`start-line`, `loadgen`, `verify`) | maximum concurrent copies observed is 1 |
| `test_worker_spools_disjoint` | R13 | fake transfers record the path of every open spool file | no path open in two workers at once; paths under `w1`..`w5` |
| `test_concurrent_disk_reserve` | R14 | `disk_usage().free` = 2689925119, then 2689925120 | first: every worker object `disk_space` / `spool reserve`; second: no `disk_space` gap |
| `test_gap_cap_under_workers` | R15, R17 | 150 failing objects across workers | `len(gaps) == 100`; last gap `diagnostics_truncated` / `diagnostic count` |
| `test_interrupt_stops_workers` | R16 | `KeyboardInterrupt` raised in the main thread while workers wait | all fake helpers stopped; gap `interrupted` / `inventory interrupted`; no marker; exit 1 |
| `test_shared_deadline` | R15 | fake clock passes the deadline mid-run | no copy starts after the deadline in any worker; remaining objects `deadline` |

## 7. Deployment assumptions

Each stays under section 13 until Hari accepts it by name.

- D1. The CLI's bundled `azure-storage-blob` `12.29.0b1` accepts the calls in `test_helper_sdk_calls` and sends `If-Match` and `If-None-Match: *` as the current Learn reference describes. Evidence: Learn reference for the current package (section 8), read 2026-11-18; the pinned tag's source was not read. No test covers the real SDK; the next C03 smoke run is the first evidence, taken on fresh connections and under no Kafka load: `scenario.yml` runs the archive step (line 237) after the phase's Jobs have finished and before "Delete this run's worker Jobs" (line 256).
- D2. `az account get-access-token --resource https://storage.azure.com/` through the wrapper returns a token whose identity has the same blob data access that `--auth-mode login` uses today. Evidence: the CLI documents that `--auth-mode login` "will directly use your login credentials"; the archive smoke's SDK observation uses this token path only after a failure, and no recorded run shows it used for a write. Not measured; first evidence at the next C03 smoke run, on a fresh connection, under no load.
- D3. The runner's spool file system has at least 2689925120 free bytes during the consumer archive. Evidence: none read, idle or under load; the runner label comes from the repository variable `PARCHWELL_RUNNER_LABEL` (`scenario.yml` line 60). A shortfall records `disk_space` gaps rather than corrupting evidence.
- D4. SDK requests with five helpers bring a consumer phase under 600 s. This is the quantity issue 548 asks to measure; it is unmeasured. It is measured at the next C03 smoke run, on fresh helper connections, after the workload has stopped.

## 8. Acceptance commands

Each pull request:
- `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` passes; it includes the phase suite through `infra/scripts/tests/test_phase_bridge.py`. Evidence: the CI log of the required `.NET verification` check at the head commit.
- `python3 -m unittest -v infra/scripts/phase/tests/test_archive_sdk.py` (pull request 1) or `test_archive_workers.py` (pull request 2) lists every test in section 6 as `ok`.
- An ASCII scan of changed files finds no non-ASCII byte.

After pull request 2 merges (section 11): the next C03 smoke run's two archive receipts.

## 9. Documentation changes (`docs/operations/archive-smoke.md`, a protected path)

Pull request 1, line 91: replace "It uses the existing serialized workflow CLI session." with:

> It obtains a storage access token through the existing serialized workflow CLI session and moves blob bytes through the pinned Blob SDK in the CLI's own Python interpreter, under [the issue 548 transport decision](<link to the decision that answers open question 1>).

Pull request 1, line 101, after "A transfer has one helper pass under the aggregate deadline;": insert "each storage call that the approved specification counts as one CLI call is one SDK helper request, bounded by 60 seconds and the aggregate deadline;".

Pull request 2, append to the consumer paragraph (line 107):

> For C01, C03 and C10 the archive collects repeats 1 to 5 in five concurrent workers, each with its own spool directory and SDK helper, then collects the phase-level Jobs. That path requires five times the single-worker spool reserve. When 100 or more gaps occur, which ones survive the cap can differ between runs.

## 10. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: the objects per phase, bounded by `ArchiveMaxObjects` (1024) and the 600 s deadline, with exhaustion action "record the gap and continue" (existing). Worker count is fixed at 5. Implementation and tests have no runtime-sized step.

(b) Stricter than its authority: pull request 2's spool reserve is five times the specification's single-object reserve. Looser: pull request 1 replaces the specification's CLI transport (line 212), which is open question 1.

(c) Dependencies neither on `main` nor in the pull requests: the real SDK behaviour (D1), the token's data access (D2), runner disk (D3) and the timing (D4). The token output format is on `main` (`smoke.py` lines 413-424) and is the CLI's documented output, cited at archive-smoke.md line 68.

## 11. Deferred work

- Measurement at the next C03 smoke run. Evidence: none yet (D4). On the verdict path: yes, it gates the #508 batch. Deferral is permitted because it needs a paid dispatch after both pull requests merge. Responsible: the #347 control session, dispatching on `main` under its own authority. Completion evidence: both phases' "Archive phase evidence" step durations from the run's job API, and receipts with no `deadline` gap. Exhaustion action: if a `deadline` gap remains, push no further change and hand off to Hari with the durations; do not raise `ArchiveDeadlineSeconds`.
- Concurrency for native-monitoring collection. Evidence: 25 index objects per phase failed in the smoke run, so their transfer cost is unknown; issue 549 owns that code. Not on the verdict path of this issue unless D4 fails. Responsible: issue 549's plan, or a follow-up after the measurement. Gate: none from this plan.

## 12. Sources

Tool: Microsoft Learn MCP server (`microsoft_docs_search`), 2026-11-18.
- Conditional headers, `If-Match` and `If-None-Match: *` ("perform the operation only if the resource doesn't exist"): https://learn.microsoft.com/rest/api/storageservices/specifying-conditional-headers-for-blob-service-operations
- `BlobClient.upload_blob` `overwrite=False` fails with `ResourceExistsError`; `etag` and `match_condition`; `max_concurrency`: https://learn.microsoft.com/python/api/azure-storage-blob/azure.storage.blob.blobclient
- `BlobServiceClient` constructor, default token audience `https://storage.azure.com/`: https://learn.microsoft.com/python/api/azure-storage-blob/azure.storage.blob.blobserviceclient
- Access token default lifetime "a random value ranging between 60-90 minutes": https://learn.microsoft.com/entra/identity-platform/access-tokens#token-lifetime
- `az storage blob download` `--if-match`, `--max-connections`: https://learn.microsoft.com/cli/azure/storage/blob
- `Copy Blob From URL`, request headers, authorization table and remarks: https://learn.microsoft.com/rest/api/storageservices/copy-blob-from-url
- Copy between network-restricted accounts, `CannotVerifyCopySource`: https://learn.microsoft.com/troubleshoot/azure/azure-storage/blobs/connectivity/copy-blobs-between-storage-accounts-network-restriction
- Thread safety of Python Blob SDK clients: not found for Python. The Learn thread-safety page is for .NET (https://learn.microsoft.com/dotnet/azure/sdk/thread-safety). The design therefore uses one client per helper process, serving one request at a time.

## 13. Open questions for Hari

1. Transport. Amend the approved specification's line 212 so the phase archive moves blob bytes through the pinned Blob SDK in a helper process, with a storage token obtained through the serialized CLI session, as pull request 1 describes? Alternatives: (b) change `scripts/azure-cli-session/az` so storage commands share the lock and only renewal is exclusive; this changes the wrapper used by every Azure job, contradicts line 215, keeps one CLI process start per call, and concurrent CLI use of one configuration directory is unverified; (c) raise the deadline, which issue 548 records Hari declined; (d) server-side copy, evaluated in section 1.2 and not recommended. Recommendation: (a). Without (a) or (b), pull request 2 gains nothing, because the wrapper lock serialises every call.
2. Build pull request 2 before measuring? Recommendation: yes, so one smoke run measures the final design; pull request 1 alone may suffice, but finding that out costs a second paid smoke run.
3. Accept deployment assumptions D1 to D4 by name, with the next C03 smoke run as their first evidence.

## 14. Size estimate

Estimate, 2026-11-18, basis: the files in section 2 and the test tables in section 6.
- Pull request 1: 5 files, 600 to 900 changed lines.
- Pull request 2: 3 files, 400 to 600 changed lines.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2.
