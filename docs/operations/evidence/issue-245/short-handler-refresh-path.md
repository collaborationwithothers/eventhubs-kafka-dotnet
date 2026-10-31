# Issue 245: short-handler-refresh-path execution receipt

Source commit: `8cbde949d8fca424dda33accbd01c1a995495678`
Branch: `codex/issue-245-short-handler-refresh-path`

## Authority and scope

Deliverable: one implementation pull request for the exact Revision 2 plan at [issue comment 6532455703](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6532455703), body SHA-256 `82d125c082cbc0b96fbda64643481966309172a1b4ba60f4391f5bf7e0adea9b`. The independent plan check is [comment 6540399226](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6540399226), updated `2026-10-31T07:04:15Z`, with no blocking findings and the required workflow-selector P2 correction. Scope decisions are [consolidated decision 6532364305](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6532364305) and [workload/isolation decision 6540257398](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6540257398). Hari's exact Revision 2 implementation and offline-only approval is recorded at [approval comment 6540478275](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6540478275).

The authorized work is implementation plus offline verification. Preserve the plan's safety and claim boundaries: no workflow dispatch, live collection or manual smoke, Azure action, credential acquisition, token-content read, live spend, governance or label change, scientific schema/expected/results change, or whole-archive load. The future live test remains separately gated. The workflow copies remain byte-identical; Hari merges protected workflow files.

## Execution card

- **Deliverable:** all files, APIs, tests, operations documentation and bounded diagnostic behavior specified by the approved Revision 2 plan, including the exact P2 correction in the plan check: wrap each complete existing workflow job selector as `(P) && F` in both workflow copies; specifically parenthesize the `probe-batch || probe-batch-2` selector. Retain admitted-run teardown/stop predicates.
- **Current authorization:** Hari approved the exact plan revision for implementation and offline checks only. This does not authorize dispatch, live work, Azure access, credentials, token-content inspection, or spend.
- **Mandatory gates:** finish implementation before acceptance testing; run each approved plan command once on the finished diff, with fresh export directories and tests without skips; complete the existing build/analyzer/architecture, coverage, worker-image, offline shell/chart/docs and mirror checks. Record actual commands, source revisions and results here. No generated output is committed. Coordinator owns authority freshness, cold reviews, publication, push and handoff; no commit or push before the coordinator checkpoint. Hari merges protected workflow changes. Hosted final-head CI and code/security reviews remain separate mandatory publication gates.
- **Exact verification commands:** the commands below are from plan section “Commands from repo root”; replace only `<installed-dotnet-coverage-18.12.0-path>` with the already installed documented executable path. Run the complete list once after implementation, using a new `mktemp` export directory.

```bash
handler_acceptance_dir=$(mktemp -d /tmp/issue245-handler-acceptance.XXXXXXXX)
export PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY="$handler_acceptance_dir/actor"
export PARCHWELL_V2_CONTRACT_EXPORT="$handler_acceptance_dir/judge"
export PARCHWELL_PROBE_V2_METADATA_FIXTURE_DIRECTORY="$handler_acceptance_dir/metadata-actor"
export PARCHWELL_PROBE_V2_HANDLER_REFRESH_FIXTURE_DIRECTORY="$handler_acceptance_dir/handler-actors"
./scripts/verify-dotnet.sh
python3 infra/scripts/phase/tests/check_probe_v2_producers.py "$PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY" "$PARCHWELL_V2_CONTRACT_EXPORT" "$PARCHWELL_PROBE_V2_METADATA_FIXTURE_DIRECTORY" --handler-refresh-actor-export "$PARCHWELL_PROBE_V2_HANDLER_REFRESH_FIXTURE_DIRECTORY"
./scripts/verify-kafka-coverage.sh <installed-dotnet-coverage-18.12.0-path>
./scripts/verify-worker-images.sh
python -m unittest discover -s infra/scripts/phase/tests
python -m unittest discover -s infra/scripts/tests
python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh
python3 charts/parchwell/tests/test_probe_v2_chart.py
python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh
npm ci --ignore-scripts
npm run test:docs
npm run check:docs
cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt
git diff --check
```

- **Earliest authorized publication checkpoint:** after implementation is complete and all listed offline gates pass, provide the finished diff, receipt and results to the coordinator for the required cold-review process and publication decision. This receipt does not authorize a push or publication by the implementer.
- **Review and correction budget:** at most two comprehensive and two focused cold-review rounds cumulatively; coordinator manages reviews. Offline verification allows one consolidated correction batch of at most ten root causes. If another gate fails after that batch, a failure needs new scope, or review budget/focus is exhausted, preserve work on the permitted `-wip` path only when directed by the coordinator and hand off with the unresolved gate and evidence. No live acceptance work is in this authorization.

## Implementation and verification record

Implementation files and the named offline test/export paths were authored on the branch. The configured implementation model was **gpt-6-luna, high reasoning effort**. Coordinator preflight confirmed .NET SDK 10.0.402 at `/tmp/issue245/dotnet`, `dotnet-coverage` 18.12.0 at `/tmp/issue245/coverage/dotnet-coverage`, pinned CI tools at `/tmp/issue245/tools`, and the managed local Docker daemon at Server 28.4.0. No Docker endpoint/context/TLS selectors or proxy/CA settings were changed.

The source receipt at `/tmp/issue245/sources/source-receipt.md` and manifest verified the official Confluent.Kafka/librdkafka 2.16.0 and Azure.Core 1.63.0 source pins described in the plan: [Confluent absolute expiry API](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ClientExtensions.cs#L39-L91), [native expiry scheduling](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_sasl_oauthbearer.c#L365-L450), and [Azure SDK acquisition path](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Core_1.63.0/sdk/core/Azure.Core/src/Identity/MsalConfidentialClient.cs#L173-L216). The actual callback measurement was not performed; the native scheduling test is an offline acceptance gate. No credential was acquired and no token content was read.

### Predicate and evidence map

The plan adds no scientific-verifier predicate. `src/Parchwell.Verifier` and scientific expected/results data were not changed. The raw actor transport gate verifies the original scientific registration/capture/repeat field sets and byte identity while separately checking the v2 diagnostic shape.

| Requirement area | Authored evidence |
| --- | --- |
| Absolute cap, recomputation, actual/effective expiry, invalid token and install failure behavior | `tests/Parchwell.Kafka.Tests/Authentication/OAuthBearerSessionTests.cs`, `KafkaAuthenticationDependencyInjectionTests.cs`, `KafkaAuthenticationLifecycleObservationTests.cs` |
| SDK invocation/return seam, cached response, failure and observer isolation | `tests/Parchwell.Kafka.Tests/Authentication/WorkloadIdentityTokenProviderTests.cs`; native real-client polling in `KafkaOAuthBearerNativeClientTests.cs` |
| Same-client attachment guard | `tests/Parchwell.Kafka.Tests/Authentication/KafkaAuthenticationAttachmentTests.cs` |
| Helper timestamp cutoff, late raw returns, service fields, null/empty values, duplicate observations, error/nonmatch limits, cancellation and cleanup suppression | `tests/Parchwell.LoadGen.Tests/SingleActionV2/ProbeV2HandlerRefreshPathTests.cs` |
| Worker progress admission, producer/consumer actor rows, cancellation deadline shortening, same-client operation isolation, Seek overrun and no further reads at the worker cap | `tests/Parchwell.LoadGen.Tests/SingleActionV2/ProbeV2LifecycleTests.cs` |
| Local same-client PLAINTEXT delivery and Seek/readback for both empty and null values | `tests/Parchwell.LoadGen.Tests/SingleActionV2/ProbeV2ComponentTests.cs` |
| Disabled v1 omission, enabled v2 raw fields, fallback, strict option parsing and actor tuple guard | `ProbeV2AuthenticationMetadataTests.cs`, `ProbeV2OptionsTests.cs` |
| Frozen phase flag, strict parsing, preparation mismatch/stop rejection and two-actor-only transport | `infra/scripts/phase/tests/test_phase.py`, `test_probe_v2.py`, and actual host export validation in `check_probe_v2_producers.py` |
| Workflow admission and the check's parenthesized selector correction | `infra/scripts/phase/tests/test_workflow.py`, `WorkflowAuthenticationContractTests.cs`, workflow mirror and offline expected-input contract |
| Chart schema/template positive and negative tuple cases | `charts/parchwell/tests/test_probe_v2_chart.py` and offline chart acceptance |

These are fixture-specific offline claims. The unreachable-loopback native test does not establish broker service, and the local Kafka test is PLAINTEXT; neither claims combined OAuth plus service proof.

### Acceptance evidence

The initial finished-diff matrix used fresh exports in `/tmp/issue245-handler-acceptance.OvZxkoPT`. Per-command raw output is under `/tmp/issue245/acceptance-logs-initial/`; exit codes are in `status.txt`.

| Gate | Initial result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Failed during build on four findings: missing constructor XML parameter documentation, CA1068 on the plan-specified observer signature and private helper ordering, and CA1512 exact-cap throw guidance. |
| `check_probe_v2_producers.py` | Failed because the .NET gate had not produced the expected exports. |
| `verify-kafka-coverage.sh` | Failed with zero tests because the .NET build output was absent. |
| `verify-worker-images.sh` | Failed on the same .NET build findings. |
| Phase Python tests | Failed on two workflow-test roots: expression translation for the new `!inputs.test_handler_refresh_path` factor and an outdated batch-selector expectation. |
| Script Python tests | Same two workflow-test roots as the phase suite. |
| `check-offline.sh` | Passed; workflow lint and shell checks passed, including chart acceptance. |
| `test_probe_v2_chart.py` | Passed: 150 positive and 125 negative cases. |
| `check-loadgen-host.sh` | Failed on the same .NET build findings. |
| `npm ci --ignore-scripts` | Passed. |
| `npm run test:docs` | Passed: 28 tests, 0 failed, 0 skipped. |
| `npm run check:docs` | Passed with 8 advisory warnings; the command notes external URLs were not checked. The execution receipt was untracked at this point and was not included in that scan. |
| Workflow mirror `cmp` | Passed. |
| `git diff --check` | Passed on the pre-correction diff. |

One consolidated correction batch addressed those six roots. The immediate post-correction `./scripts/verify-dotnet.sh` failed again, so the plan's correction budget is exhausted and work stopped without another edit or test loop. The stopping run is `/tmp/issue245/acceptance-logs-final-dotnet.log` with status in `/tmp/issue245/acceptance-logs-final-status.txt`; its new findings are:

- xUnit1051 cancellation-token analyzer failure in `KafkaAuthenticationDependencyInjectionTests.cs`;
- xUnit1026 unused `actualMilliseconds` in the capped expiry theory;
- three CS1503 mismatches between raw monotonic timestamps and UTC `DateTimeOffset` helper fields;
- `ProbeV2SessionFactory.CreateHandlerRefreshPath` references an unavailable `options` identifier and has a CA1822 static-member finding;
- CA1068 ordering on the helper `Slice` cancellation-token parameter; and
- CA1822 on the helper `Matches` method.

That is six remaining root causes across nine diagnostics. The cumulative offline correction budget is exhausted at **1/1**; no cold review rounds have run (**0/2 comprehensive, 0/2 focused**) and no hosted final-head checks have run. There is no implementation PR or reviewed implementation head.

The post-correction collector, coverage, worker-image, Python, loadgen-host, staged-receipt documentation and final diff gates were not run because the first post-correction gate failed and the coordinator directed a stop. Initial results for unchanged chart, offline shell, npm, docs and mirror inputs are preserved above, but do not establish a passing final head. The official source-pinning record and file hashes are in `/tmp/issue245/sources/source-receipt.md` and `/tmp/issue245/sources/manifest.json`. No commit, push, pull request, dispatch, live action, Azure access, credential acquisition, spend, governance/label change or scientific result publication occurred. Coordinator review and preservation are required before any further implementation work; no further correction is authorized by this receipt.
